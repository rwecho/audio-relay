using AudioRelay.Audio;

namespace AudioRelay.Audio.Tests;

public class LevelMeterTests
{
    [Fact]
    public void Silence_YieldsZeroRmsAndPeak()
    {
        var meter = new LevelMeter();
        meter.Update(new float[480]);
        Assert.Equal(0, meter.LatestRms, 6);
        Assert.Equal(0, meter.LatestPeak, 6);
    }

    [Fact]
    public void FullScale_YieldsUnityRmsAndPeak()
    {
        var meter = new LevelMeter();
        meter.Update(Enumerable.Repeat(1f, 480).ToArray());
        Assert.Equal(1.0, meter.LatestRms, 6);
        Assert.Equal(1.0, meter.LatestPeak, 6);
    }

    [Fact]
    public void HalfScale_YieldsHalfRms()
    {
        var meter = new LevelMeter();
        meter.Update(Enumerable.Repeat(0.5f, 480).ToArray());
        Assert.Equal(0.5, meter.LatestRms, 6);
        Assert.Equal(0.5, meter.LatestPeak, 6);
    }

    [Fact]
    public void PeakTracksAbsoluteAmplitude()
    {
        var meter = new LevelMeter();
        meter.Update(Enumerable.Repeat(-0.8f, 100).ToArray());
        Assert.Equal(0.8, meter.LatestPeak, 6);
    }

    [Fact]
    public void Update_IgnoresEmptyChunk()
    {
        var meter = new LevelMeter();
        meter.Update(Array.Empty<float>());
        Assert.Equal(0, meter.LatestRms, 6);
    }

    [Fact]
    public void Snapshot_ReturnsOldestToNewestInInsertionOrder()
    {
        var meter = new LevelMeter(capacity: 4);
        foreach (var v in new[] { 0.10f, 0.20f, 0.30f })
            meter.Update(Enumerable.Repeat(v, 10).ToArray());

        var snap = meter.Snapshot(3);
        Assert.Equal(new[] { 0.10f, 0.20f, 0.30f }, snap.Select(f => (float)Math.Round(f, 2)).ToArray());
    }

    [Fact]
    public void Snapshot_WrapsAroundCapacityKeepingMostRecent()
    {
        var meter = new LevelMeter(capacity: 3);
        foreach (var v in new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f }) // overwrites oldest two
            meter.Update(Enumerable.Repeat(v, 10).ToArray());

        var snap = meter.Snapshot(3);
        Assert.Equal(new[] { 0.3f, 0.4f, 0.5f }, snap.Select(f => (float)Math.Round(f, 2)).ToArray());
    }

    [Fact]
    public void Snapshot_CountLargerThanCapacity_ReturnsAllCapacity()
    {
        var meter = new LevelMeter(capacity: 4);
        meter.Update(Enumerable.Repeat(0.7f, 10).ToArray());
        Assert.Equal(4, meter.Snapshot(100).Length);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new LevelMeter(0));
}
