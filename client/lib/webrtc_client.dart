import 'dart:async';
import 'package:flutter_webrtc/flutter_webrtc.dart';
import 'signaling_client.dart';

enum RtcStatus { connecting, playing, reconnecting, failed }

/// WebRTC client (offerer, recv-only). Exchanges SDP via the PC's signaling endpoint,
/// plays the incoming audio track, and auto-reconnects with exponential backoff when the
/// peer connection drops (screen lock, background, network blip). UNVERIFIED — needs
/// flutter SDK + E2E.
class WebRtcClient {
  final SignalingClient _signaling;
  RTCPeerConnection? _pc;
  MediaStreamTrack? _audioTrack;
  bool _muted = false;
  bool _disposed = false;
  int _attempts = 0;
  Timer? _reconnectTimer;

  final _statusController = StreamController<RtcStatus>.broadcast();
  Stream<RtcStatus> get status => _statusController.stream;

  WebRtcClient(this._signaling);

  Future<void> connect() async {
    if (_disposed) return;
    _statusController.add(RtcStatus.connecting);
    await _doConnect();
  }

  Future<void> _doConnect() async {
    try {
      await _pc?.close();
      await _pc?.dispose();
      _pc = await createPeerConnection({'iceServers': <Map<String, dynamic>>[]});

      await _pc!.addTransceiver(
        kind: RTCRtpMediaType.RTCRtpMediaTypeAudio,
        init: RTCRtpTransceiverInit(direction: TransceiverDirection.RecvOnly),
      );

      _pc!.onTrack = (RTCTrackEvent event) {
        if (event.track.kind == 'audio') {
          _audioTrack = event.track;
          event.track.enabled = !_muted;
        }
      };

      _pc!.onConnectionState = (state) {
        if (state == RTCPeerConnectionState.RTCPeerConnectionStateConnected) {
          _attempts = 0;
          _statusController.add(RtcStatus.playing);
        } else if (state == RTCPeerConnectionState.RTCPeerConnectionStateDisconnected ||
            state == RTCPeerConnectionState.RTCPeerConnectionStateFailed) {
          _scheduleReconnect();
        }
      };

      final offer = await _pc!.createOffer(<String, dynamic>{});
      await _pc!.setLocalDescription(offer);

      // Non-trickle: wait for ICE gathering to embed host candidates.
      final gathered = Completer<void>();
      _pc!.onIceGatheringState = (s) {
        if (s == RTCIceGatheringState.RTCIceGatheringStateComplete && !gathered.isCompleted) {
          gathered.complete();
        }
      };
      if (_pc!.iceGatheringState == RTCIceGatheringState.RTCIceGatheringStateComplete) {
        gathered.complete();
      }
      await gathered.future.timeout(const Duration(seconds: 3));

      final localOffer = await _pc!.getLocalDescription();
      final answerSdp = await _signaling.createAnswer(localOffer!.sdp!);
      await _pc!.setRemoteDescription(RTCSessionDescription(answerSdp, 'answer'));
      if (_audioTrack != null) _audioTrack!.enabled = !_muted;
    } catch (_) {
      _scheduleReconnect();
    }
  }

  void _scheduleReconnect() {
    if (_disposed) return;
    _statusController.add(RtcStatus.reconnecting);
    _attempts++;
    final seconds = _attempts > 5 ? 5 : _attempts; // backoff capped at 5s
    _reconnectTimer?.cancel();
    _reconnectTimer = Timer(Duration(seconds: seconds), () {
      if (!_disposed) _doConnect();
    });
  }

  bool get isMuted => _muted;
  void setMuted(bool muted) {
    _muted = muted;
    _audioTrack?.enabled = !muted;
  }

  Future<void> close() async {
    _disposed = true;
    _reconnectTimer?.cancel();
    await _pc?.close();
    await _pc?.dispose();
    _pc = null;
    _audioTrack = null;
    await _statusController.close();
  }
}
