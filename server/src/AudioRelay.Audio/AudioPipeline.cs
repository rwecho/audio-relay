using AudioRelay.Codec;

namespace AudioRelay.Audio;

/// <summary>
/// Wires the real-time audio path: capturer (48k float) → framing → Opus encode → sink.
/// Driven entirely by <see cref="IAudioCapturer.SamplesAvailable"/> (the real-time capture
/// clock), so Opus frames leave at capture rate with no sender-side buffering. The sink
/// (WebRTC) hands each Opus frame to libdatachannel, which packetizes it into RTP
/// (timestamps + sequence numbers + SR reporter) and sends it.
/// </summary>
public sealed class AudioPipeline : IDisposable
{
    private readonly IAudioCapturer _capturer;
    private readonly IOpusSink _sink;
    private readonly FloatToInt16Framer _framer;
    private readonly OpusEncoderAdapter _encoder;
    private readonly GainStage _gain = new();
    private bool _running;

    public AudioPipeline(IAudioCapturer capturer, IOpusSink sink, OpusEncoderAdapter? encoder = null)
    {
        _capturer = capturer;
        _sink = sink;
        _encoder = encoder ?? new OpusEncoderAdapter(capturer.Format.SampleRate, capturer.Format.Channels);
        _framer = new FloatToInt16Framer(_encoder.SamplesPerChannel, capturer.Format.Channels);
        _capturer.SamplesAvailable += OnSamples;
    }

    public bool IsRunning => _running;

    /// <summary>Volume multiplier (0 = mute, 1 = unity). Smoothly ramped to avoid pops.</summary>
    public double Volume
    {
        get => _gain.Volume;
        set => _gain.Volume = (float)value;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _gain.Reset(); // fade in from silence to avoid a startup pop
        _capturer.Start();
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _capturer.Stop();
    }

    private void OnSamples(object? sender, ArraySegment<float> samples)
    {
        if (!_running) return;

        _gain.Apply(samples.AsSpan()); // in-place; capturer supplies a fresh buffer per chunk
        _framer.Feed(samples.AsSpan(), frame =>
        {
            byte[] opus = _encoder.Encode(frame);
            _sink.Send(opus);
        });
    }

    public void Dispose()
    {
        Stop();
        _capturer.SamplesAvailable -= OnSamples;
    }
}
