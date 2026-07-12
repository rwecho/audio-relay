using AudioRelay.Audio;
using AudioRelay.Codec;

namespace AudioRelay.Audio.Tests;

public class AudioPipelineTests
{
    private const int FrameLength = 960 * 2; // 20ms stereo @ 48kHz

    [Fact]
    public void Start_ForwardsToCapturer()
    {
        var capturer = new FakeCapturer();
        var pipeline = new AudioPipeline(capturer, new CollectingSink());

        pipeline.Start();

        Assert.Equal(1, capturer.StartCount);
        Assert.True(pipeline.IsRunning);
    }

    [Fact]
    public void Stop_ForwardsToCapturer()
    {
        var capturer = new FakeCapturer();
        var pipeline = new AudioPipeline(capturer, new CollectingSink());
        pipeline.Start();

        pipeline.Stop();

        Assert.Equal(1, capturer.StopCount);
        Assert.False(pipeline.IsRunning);
    }

    [Fact]
    public void OneFrame_EmitsExactlyOneOpusFrame()
    {
        var capturer = new FakeCapturer();
        var sink = new CollectingSink();
        var pipeline = new AudioPipeline(capturer, sink);
        pipeline.Start();

        capturer.Emit(new float[FrameLength]);

        Assert.Single(sink.Frames);
    }

    [Fact]
    public void TwoFrames_EmitsTwoOpusFrames()
    {
        var capturer = new FakeCapturer();
        var sink = new CollectingSink();
        var pipeline = new AudioPipeline(capturer, sink);
        pipeline.Start();

        capturer.Emit(new float[FrameLength * 2]);

        Assert.Equal(2, sink.Frames.Count);
    }

    [Fact]
    public void PartialFrame_EmitsNothing()
    {
        var capturer = new FakeCapturer();
        var sink = new CollectingSink();
        var pipeline = new AudioPipeline(capturer, sink);
        pipeline.Start();

        capturer.Emit(new float[FrameLength / 2]);

        Assert.Empty(sink.Frames);
    }

    [Fact]
    public void EachEmittedFrame_IsValidSizedOpus()
    {
        var capturer = new FakeCapturer();
        var sink = new CollectingSink();
        var pipeline = new AudioPipeline(capturer, sink);
        pipeline.Start();
        capturer.Emit(new float[FrameLength * 2]);

        foreach (var frame in sink.Frames)
            Assert.InRange(frame.Length, 1, 1275);
    }

    [Fact]
    public void SamplesEmittedWhileNotRunning_AreIgnored()
    {
        var capturer = new FakeCapturer();
        var sink = new CollectingSink();
        var pipeline = new AudioPipeline(capturer, sink);

        capturer.Emit(new float[FrameLength]); // never started

        Assert.Empty(sink.Frames);
    }

    [Fact]
    public void Dispose_UnsubscribesFromCapturer()
    {
        var capturer = new FakeCapturer();
        var sink = new CollectingSink();
        var pipeline = new AudioPipeline(capturer, sink);
        pipeline.Start();
        pipeline.Dispose();

        capturer.Emit(new float[FrameLength]);

        Assert.Empty(sink.Frames);
    }

    private sealed class FakeCapturer : IAudioCapturer
    {
        public AudioFormat Format { get; set; } = new(48000, 2);
        public int StartCount;
        public int StopCount;
        public event EventHandler<ArraySegment<float>>? SamplesAvailable;
        public event EventHandler? DefaultDeviceChanged;

        public void Start() => StartCount++;
        public void Stop() => StopCount++;
        public void Emit(float[] samples) => SamplesAvailable?.Invoke(this, samples);
    }

    private sealed class CollectingSink : IOpusSink
    {
        public List<byte[]> Frames { get; } = new();
        public void Send(ReadOnlySpan<byte> opusFrame) => Frames.Add(opusFrame.ToArray());
    }
}
