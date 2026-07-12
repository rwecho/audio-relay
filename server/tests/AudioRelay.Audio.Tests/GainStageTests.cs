using AudioRelay.Audio;

namespace AudioRelay.Audio.Tests;

public class GainStageTests
{
    [Fact]
    public void Volume_ClampsNegativeToZero()
    {
        var g = new GainStage { Volume = -0.5f };
        Assert.Equal(0f, g.Volume);
    }

    [Fact]
    public void Reset_SetsAppliedToSilence()
    {
        var g = new GainStage();
        g.Apply(new float[10000]); // ramp up first
        Assert.True(g.Applied > 0f);

        g.Reset();

        Assert.Equal(0f, g.Applied);
    }

    [Fact]
    public void Apply_RampsUpFromSilenceTowardTarget()
    {
        var g = new GainStage { Volume = 1f };
        g.Reset();
        var samples = new float[10000];

        g.Apply(samples);

        Assert.True(g.Applied > 0f);
        Assert.True(g.Applied <= 1f);
    }

    [Fact]
    public void Apply_AtSteadyState_ScalesAllSamplesByVolume()
    {
        var g = new GainStage { Volume = 0.5f };
        g.Apply(new float[100000]); // reach steady state
        Assert.Equal(0.5f, g.Applied, 3);

        var samples = new float[] { 1f, -1f, 0.5f };
        g.Apply(samples);

        Assert.Equal(0.5f, samples[0], 3);
        Assert.Equal(-0.5f, samples[1], 3);
        Assert.Equal(0.25f, samples[2], 3);
    }

    [Fact]
    public void Apply_EventuallyReachesTargetVolume()
    {
        var g = new GainStage { Volume = 1f };
        g.Reset();

        g.Apply(new float[10_000_000]); // plenty to fully ramp

        Assert.Equal(1f, g.Applied, 3);
    }
}
