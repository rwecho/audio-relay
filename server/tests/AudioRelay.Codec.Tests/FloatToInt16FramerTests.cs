using AudioRelay.Codec;

namespace AudioRelay.Codec.Tests;

public class FloatToInt16FramerTests
{
    private const int SamplesPerChannel = 960;
    private const int Channels = 2;
    private const int FrameLength = SamplesPerChannel * Channels; // 1920

    private static List<short[]> Collect(FloatToInt16Framer framer, float[] input)
    {
        var frames = new List<short[]>();
        framer.Feed(input, frame => frames.Add(frame.ToArray()));
        return frames;
    }

    [Fact]
    public void Constructor_RejectsNonPositiveDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FloatToInt16Framer(0, Channels));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FloatToInt16Framer(SamplesPerChannel, 0));
    }

    [Fact]
    public void Feed_BelowFrameLength_EmitsNothing_AndBuffers()
    {
        var framer = new FloatToInt16Framer(SamplesPerChannel, Channels);

        var frames = Collect(framer, new float[100]);

        Assert.Empty(frames);
        Assert.Equal(100, framer.PendingFloats);
    }

    [Fact]
    public void Feed_ExactlyFrameLength_EmitsOneFrame_AndClearsBuffer()
    {
        var framer = new FloatToInt16Framer(SamplesPerChannel, Channels);

        var frames = Collect(framer, new float[FrameLength]);

        Assert.Single(frames);
        Assert.Equal(FrameLength, frames[0].Length);
        Assert.Equal(0, framer.PendingFloats);
    }

    [Fact]
    public void Feed_ConvertsFloatToInt16_Correctly()
    {
        var framer = new FloatToInt16Framer(2, 1); // frame length 2

        var first = Collect(framer, new[] { 0.0f, 0.5f });
        var second = Collect(framer, new[] { 1.0f, -1.0f });

        Assert.Equal(0, first[0][0]);
        Assert.Equal(16383, first[0][1]);        // (int)(0.5 * 32767)
        Assert.Equal(32767, second[0][0]);
        Assert.Equal(-32767, second[0][1]);
    }

    [Fact]
    public void Feed_ClampsOverflowToInt16Range()
    {
        var framer = new FloatToInt16Framer(2, 1);

        var frames = Collect(framer, new[] { 2.0f, -2.0f });

        Assert.Equal(32767, frames[0][0]);
        Assert.Equal(-32768, frames[0][1]);
    }

    [Fact]
    public void Feed_EmitsMultipleFramesFromOneCall_BuffersRemainder()
    {
        var framer = new FloatToInt16Framer(SamplesPerChannel, Channels);
        // 2.5 frames worth of samples
        var input = new float[FrameLength * 2 + FrameLength / 2];

        var frames = Collect(framer, input);

        Assert.Equal(2, frames.Count);
        Assert.Equal(FrameLength / 2, framer.PendingFloats);
    }

    [Fact]
    public void Feed_AccumulatesAcrossChunkedCalls()
    {
        var framer = new FloatToInt16Framer(SamplesPerChannel, Channels);
        var frames = new List<short[]>();
        int chunk = FrameLength / 10;
        for (int i = 0; i < 10; i++)
            framer.Feed(new float[chunk], frame => frames.Add(frame.ToArray()));

        Assert.Single(frames);
        Assert.Equal(0, framer.PendingFloats);
    }

    [Fact]
    public void Feed_PreservesSampleOrderAcrossCalls()
    {
        var framer = new FloatToInt16Framer(4, 1);
        var frames = new List<short[]>();
        framer.Feed(new[] { 0.1f, 0.2f }, f => frames.Add(f.ToArray()));
        framer.Feed(new[] { 0.3f, 0.4f }, f => frames.Add(f.ToArray()));
        framer.Feed(new[] { 0.5f, 0.6f, 0.7f, 0.8f }, f => frames.Add(f.ToArray()));

        Assert.Equal(2, frames.Count);
        Assert.Equal(Convert(0.1f), frames[0][0]);
        Assert.Equal(Convert(0.4f), frames[0][3]);
        Assert.Equal(Convert(0.5f), frames[1][0]);
        Assert.Equal(Convert(0.8f), frames[1][3]);
    }

    private static short Convert(float f) => (short)Math.Clamp((int)(f * 32767f), -32768, 32767);
}
