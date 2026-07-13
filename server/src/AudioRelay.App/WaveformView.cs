using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AudioRelay.App;

/// <summary>
/// Renders a live audio level envelope (RMS per recent chunk) as centered vertical bars.
/// Driven by the <see cref="Waveform"/> property (oldest→newest levels, 0..1), repainted via
/// AffectsRender on each update from the ~30Hz dispatcher timer.
/// </summary>
public sealed class WaveformView : Control
{
    public static readonly StyledProperty<float[]> WaveformProperty =
        AvaloniaProperty.Register<WaveformView, float[]>(nameof(Waveform));

    public float[] Waveform
    {
        get => GetValue(WaveformProperty);
        set => SetValue(WaveformProperty, value);
    }

    private readonly IBrush _barBrush = new SolidColorBrush(Color.FromArgb(255, 0x0A, 0x84, 0xFF));

    static WaveformView() => AffectsRender<WaveformView>(WaveformProperty);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var data = Waveform;
        if (data is null || data.Length == 0) return;

        double w = Bounds.Width > 0 ? Bounds.Width : Width;
        double h = Bounds.Height > 0 ? Bounds.Height : Height;
        if (w <= 0 || h <= 0) return;

        double mid = h / 2;
        double barW = w / data.Length;
        double gap = Math.Max(1, barW * 0.25);
        double drawW = Math.Max(1, barW - gap);
        for (int i = 0; i < data.Length; i++)
        {
            double mag = Math.Clamp(data[i], 0, 1) * mid;
            double x = i * barW + gap / 2;
            context.FillRectangle(_barBrush, new Rect(x, mid - mag, drawW, mag * 2));
        }
    }
}
