import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import 'signaling_client.dart';

/// Rhythm-particle field for the phone client. A ring of blue-purple particles around a glowing
/// core, pushed outward and brightened by the live bass impact, slowly rotating — mirroring the
/// Avalonia server window and the HTML browser page for a consistent product identity.
///
/// The phone has no access to the received audio samples (flutter_webrtc exposes no tap), so the
/// bass is polled from the server's GET /stats (computed server-side via FFT on the captured PCM).
/// Rotation is animated locally at ~60fps; bass is refreshed at ~10Hz.
class RhythmParticles extends StatefulWidget {
  final String url; // server base URL, e.g. http://192.168.20.110:8080
  final double width;
  final double height;
  const RhythmParticles({
    super.key,
    required this.url,
    this.width = 300,
    this.height = 180,
  });

  @override
  State<RhythmParticles> createState() => _RhythmParticlesState();
}

class _RhythmParticlesState extends State<RhythmParticles>
    with SingleTickerProviderStateMixin {
  late final AnimationController _ctrl;
  Timer? _poll;
  double _bass = 0;
  double _rotation = 0;
  late final SignalingClient _sig;
  late final List<_Particle> _particles;

  @override
  void initState() {
    super.initState();
    _sig = SignalingClient(widget.url, ''); // PIN unused for /stats
    _particles = List.generate(140, (_) => _Particle.random());
    _ctrl = AnimationController(vsync: this, duration: const Duration(seconds: 1))
      ..repeat();
    _ctrl.addListener(() => setState(() => _rotation += 0.004));
    _poll = Timer.periodic(const Duration(milliseconds: 100), (_) async {
      final b = await _sig.fetchBass();
      if (mounted) setState(() => _bass = b);
    });
  }

  @override
  void dispose() {
    _poll?.cancel();
    _ctrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => CustomPaint(
        painter: _ParticlePainter(_particles, _bass, _rotation),
        size: Size(widget.width, widget.height),
      );
}

class _Particle {
  final double angle, baseDist, baseRadius;
  final Color color;
  _Particle(this.angle, this.baseDist, this.baseRadius, this.color);

  factory _Particle.random() {
    final r = Random();
    final hue = 200 + r.nextDouble() * 60; // blue → purple
    return _Particle(
      r.nextDouble() * 2 * pi,
      14 + r.nextDouble() * 48,
      1 + r.nextDouble() * 1.8,
      HSLColor.fromAHSL(1, hue, 0.8, 0.6).toColor(),
    );
  }
}

class _ParticlePainter extends CustomPainter {
  final List<_Particle> particles;
  final double bass, rotation;
  _ParticlePainter(this.particles, this.bass, this.rotation);

  @override
  void paint(Canvas canvas, Size size) {
    final cx = size.width / 2, cy = size.height / 2;
    final b = bass.clamp(0.0, 1.0);

    // glowing core pulses with the bass
    final coreR = 5 + b * 12;
    final corePaint = Paint()
      ..color = Color.fromARGB((120 + 100 * b).round().clamp(0, 255), 60, 120, 255);
    canvas.drawCircle(Offset(cx, cy), coreR, corePaint);

    for (final p in particles) {
      final a = p.angle + rotation;
      final d = p.baseDist + b * 46;
      final rad = p.baseRadius + b * 2.4;
      final x = cx + cos(a) * d;
      final y = cy + sin(a) * d;
      final paint = Paint()
        ..color = p.color.withAlpha((110 + 120 * b).round().clamp(0, 255));
      canvas.drawCircle(Offset(x, y), rad, paint);
    }
  }

  @override
  bool shouldRepaint(covariant _ParticlePainter old) =>
      old.bass != bass || old.rotation != rotation;
}
