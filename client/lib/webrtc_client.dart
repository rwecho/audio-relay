import 'dart:async';
import 'package:flutter_webrtc/flutter_webrtc.dart';
import 'signaling_client.dart';

enum RtcStatus { connecting, playing, reconnecting, failed }

/// WebRTC client (answerer, recv-only). Gets the server's offer, answers it, plays the
/// incoming audio track, and auto-reconnects with exponential backoff when the peer
/// connection drops. UNVERIFIED — needs flutter SDK + E2E.
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

      // The server is the offerer: fetch its offer, answer it.
      final offerSdp = await _signaling.requestOffer();
      await _pc!.setRemoteDescription(RTCSessionDescription(offerSdp, 'offer'));

      final answer = await _pc!.createAnswer(<String, dynamic>{});
      await _pc!.setLocalDescription(answer);

      // Non-trickle: wait for ICE gathering to embed host candidates in our answer.
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

      final localAnswer = await _pc!.getLocalDescription();
      await _signaling.submitAnswer(localAnswer!.sdp!);
      if (_audioTrack != null) _audioTrack!.enabled = !_muted;
    } catch (_) {
      _scheduleReconnect();
    }
  }

  void _scheduleReconnect() {
    if (_disposed) return;
    _statusController.add(RtcStatus.reconnecting);
    _attempts++;
    final seconds = _attempts > 5 ? 5 : _attempts;
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
