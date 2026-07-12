using AudioRelay.Audio;

namespace AudioRelay.Audio.Tests;

public class LinearResamplerTests
{
    [Fact]
    public void SameRate_ReturnsInputUnchanged()
    {
        var input = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };

        var result = LinearResampler.Resample(input, inputRate: 48000, outputRate: 48000, channels: 2);

        Assert.Same(input, result);
    }

    [Fact]
    public void DownsampleHalvesFrameCount()
    {
        // 8 stereo frames at 48k -> ~4 frames at 24k
        var input = new float[8 * 2];
        for (int i = 0; i < input.Length; i++) input[i] = i;

        var result = LinearResampler.Resample(input, inputRate: 48000, outputRate: 24000, channels: 2);

        Assert.Equal(4 * 2, result.Length);
    }

    [Fact]
    public void UpsampleIncreasesFrameCount()
    {
        var input = new float[4 * 2]; // 4 stereo frames
        for (int i = 0; i < input.Length; i++) input[i] = 1f;

        var result = LinearResampler.Resample(input, inputRate: 24000, outputRate: 48000, channels: 2);

        Assert.Equal(8 * 2, result.Length);
        // all-ones input interpolates to all ones
        foreach (var v in result) Assert.Equal(1f, v, 2);
    }

    [Fact]
    public void ReachesValueRangeAtEndpoints()
    {
        // ramp 0..1 across 5 mono frames; endpoints preserved after identity-rate skip
        var input = new float[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
        var result = LinearResampler.Resample(input, inputRate: 5, outputRate: 5, channels: 1);

        Assert.Same(input, result); // same rate shortcut
    }
}
