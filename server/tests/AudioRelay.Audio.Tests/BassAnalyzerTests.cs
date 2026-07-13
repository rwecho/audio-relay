using AudioRelay.Audio;

namespace AudioRelay.Audio.Tests;

public class BassAnalyzerTests
{
    private const int Rate = 48000;

    private static float[] Sine(float freq, float amp = 1f, int count = 4800, int channels = 2)
    {
        var buf = new float[count * channels];
        for (int i = 0; i < count; i++)
        {
            float v = amp * MathF.Sin(2 * MathF.PI * freq * i / Rate);
            for (int c = 0; c < channels; c++) buf[i * channels + c] = v;
        }
        return buf;
    }

    [Fact]
    public void Silence_YieldsZeroBass()
    {
        var ba = new BassAnalyzer();
        ba.Update(new float[4800]); // 2400 mono frames of silence
        Assert.Equal(0, ba.LatestBass, 6);
    }

    [Fact]
    public void LowFrequency_HighBass()
    {
        var ba = new BassAnalyzer();
        ba.Update(Sine(60f, 1f)); // 60 Hz → bin ~1, squarely in the bass band
        Assert.InRange(ba.LatestBass, 0.3, 1.0);
    }

    [Fact]
    public void HighFrequency_LowBass()
    {
        var ba = new BassAnalyzer();
        ba.Update(Sine(5000f, 1f)); // 5 kHz → bin ~107, outside the bass band
        Assert.InRange(ba.LatestBass, 0, 0.1);
    }

    [Fact]
    public void BassBand_BoundaryHz_LowBass() // ~300 Hz sits just above the band
    {
        var ba = new BassAnalyzer();
        ba.Update(Sine(300f, 1f));
        Assert.InRange(ba.LatestBass, 0, 0.2);
    }

    [Fact]
    public void QuieterBass_LowerButNonZero()
    {
        var loud = new BassAnalyzer(); loud.Update(Sine(60f, 1f));
        var quiet = new BassAnalyzer(); quiet.Update(Sine(60f, 0.1f));
        Assert.True(loud.LatestBass > quiet.LatestBass);
        Assert.InRange(quiet.LatestBass, 0, loud.LatestBass);
    }

    [Fact]
    public void Update_IgnoresEmptyInput()
    {
        var ba = new BassAnalyzer();
        ba.Update(Array.Empty<float>());
        Assert.Equal(0, ba.LatestBass, 6);
    }

    [Fact]
    public void Decay_HoldsThenReleasesBass()
    {
        var ba = new BassAnalyzer();
        ba.Update(Sine(60f, 1f));
        double peak = ba.LatestBass;
        Assert.True(peak > 0);
        // feed silence afterward: peak-hold decays but never instantly to zero
        ba.Update(new float[4800]);
        Assert.True(ba.LatestBass <= peak);
        Assert.True(ba.LatestBass > 0); // still decaying, not snapped to 0
    }
}
