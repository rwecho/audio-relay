using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace AudioRelay.App;

/// <summary>
/// Rhythm particle field: a ring of blue-purple particles around a glowing core, pushed outward
/// and brightened by the live bass impact (0..1). Self-animates via an internal ~30Hz timer so it
/// keeps drifting even in silence, and reacts to <see cref="Bass"/> every frame. Mirrors the
/// HTML/Flutter particle effect for a consistent product identity.
/// </summary>
public sealed class ParticleView : Control
{
    public static readonly StyledProperty<double> BassProperty =
        AvaloniaProperty.Register<ParticleView, double>(nameof(Bass));

    public double Bass
    {
        get => GetValue(BassProperty);
        set => SetValue(BassProperty, value);
    }

    private readonly struct P(double angle, double dist, double radius, byte r, byte g, byte b)
    {
        public readonly double Angle = angle, Dist = dist, Radius = radius;
        public readonly byte R = r, G = g, B = b;
    }

    private readonly P[] _particles;
    private double _rotation;
    private readonly DispatcherTimer _timer;

    public ParticleView()
    {
        var ps = new P[140];
        var rng = new Random(42); // deterministic layout
        for (int i = 0; i < ps.Length; i++)
        {
            ToRgb(200 + rng.NextDouble() * 60, 0.8, 0.6, out byte r, out byte g, out byte b);
            ps[i] = new P(rng.NextDouble() * Math.PI * 2, 14 + rng.NextDouble() * 48, 1 + rng.NextDouble() * 1.8, r, g, b);
        }
        _particles = ps;
        Width = 300; Height = 160;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => InvalidateVisual());
        _timer.Start();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        double w = Bounds.Width > 0 ? Bounds.Width : Width;
        double h = Bounds.Height > 0 ? Bounds.Height : Height;
        if (w <= 0 || h <= 0) return;

        double cx = w / 2, cy = h / 2;
        double bass = Math.Clamp(Bass, 0, 1);
        _rotation += 0.004;

        // glowing core pulses with the bass
        double coreR = 5 + bass * 12;
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb((byte)(120 + 100 * bass), 60, 120, 255)), null,
            new Rect(cx - coreR, cy - coreR, coreR * 2, coreR * 2));

        for (int i = 0; i < _particles.Length; i++)
        {
            var p = _particles[i];
            double a = p.Angle + _rotation;
            double d = p.Dist + bass * 46;
            double rad = p.Radius + bass * 2.4;
            double x = cx + Math.Cos(a) * d;
            double y = cy + Math.Sin(a) * d;
            byte alpha = (byte)(110 + 120 * bass);
            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(alpha, p.R, p.G, p.B)), null,
                new Rect(x - rad, y - rad, rad * 2, rad * 2));
        }
    }

    private static void ToRgb(double h, double s, double l, out byte r, out byte g, out byte b)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double hp = h / 60.0;
        double x = c * (1 - Math.Abs(hp % 2 - 1));
        double r1, g1, b1;
        if (hp < 1) { r1 = c; g1 = x; b1 = 0; }
        else if (hp < 2) { r1 = x; g1 = c; b1 = 0; }
        else if (hp < 3) { r1 = 0; g1 = c; b1 = x; }
        else if (hp < 4) { r1 = 0; g1 = x; b1 = c; }
        else if (hp < 5) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }
        double m = l - c / 2;
        r = (byte)Math.Clamp((r1 + m) * 255, 0, 255);
        g = (byte)Math.Clamp((g1 + m) * 255, 0, 255);
        b = (byte)Math.Clamp((b1 + m) * 255, 0, 255);
    }
}
