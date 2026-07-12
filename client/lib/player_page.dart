import 'dart:async';
import 'package:flutter/material.dart';
import 'webrtc_client.dart';
import 'signaling_client.dart';

class PlayerPage extends StatefulWidget {
  final String url;
  final String pin;
  const PlayerPage({super.key, required this.url, required this.pin});

  @override
  State<PlayerPage> createState() => _PlayerPageState();
}

class _PlayerPageState extends State<PlayerPage> {
  late final WebRtcClient _rtc;
  StreamSubscription<RtcStatus>? _sub;
  RtcStatus _status = RtcStatus.connecting;
  bool _muted = false;

  @override
  void initState() {
    super.initState();
    _rtc = WebRtcClient(SignalingClient(widget.url, widget.pin));
    _sub = _rtc.status.listen((s) {
      if (mounted) setState(() => _status = s);
    });
    _rtc.connect();
  }

  @override
  void dispose() {
    _sub?.cancel();
    _rtc.close();
    super.dispose();
  }

  String get _statusText => switch (_status) {
        RtcStatus.connecting => '正在连接…',
        RtcStatus.playing => '正在播放',
        RtcStatus.reconnecting => '重连中…',
        RtcStatus.failed => '连接失败',
      };

  @override
  Widget build(BuildContext context) {
    final live = _status == RtcStatus.playing;
    return Scaffold(
      backgroundColor: const Color(0xFF1C1C1E),
      body: SafeArea(
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.graphic_eq,
                  size: 96, color: live ? const Color(0xFF0A84FF) : Colors.white24),
              const SizedBox(height: 24),
              Text(_statusText,
                  style: const TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.w600)),
              const SizedBox(height: 8),
              Text(widget.url, style: const TextStyle(color: Colors.white38, fontSize: 12)),
              const SizedBox(height: 40),
              IconButton(
                iconSize: 64,
                icon: Icon(_muted ? Icons.volume_off : Icons.volume_up, color: Colors.white),
                onPressed: live
                    ? () => setState(() { _muted = !_muted; _rtc.setMuted(_muted); })
                    : null,
              ),
              const SizedBox(height: 24),
              TextButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('断开', style: TextStyle(color: Colors.white54)),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
