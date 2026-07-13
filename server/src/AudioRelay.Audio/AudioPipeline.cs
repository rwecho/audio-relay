using AudioRelay.Codec;

namespace AudioRelay.Audio;

/// <summary>
/// Wires the real-time audio path: capturer (48k float) → framing → Opus encode → sink.
/// Capture and send are split so the level/waveform meter can run always-on (tracking whatever the
/// PC is playing) while the Opus encode/send is gated to only when a client is connected (saves CPU
/// when idle). Driven entirely by <see cref="IAudioCapturer.SamplesAvailable"/> (the real-time
/// capture clock), so Opus frames leave at capture rate with no sender-side buffering.
/// </summary>
public sealed class AudioPipeline : IDisposable
{
    private readonly IAudioCapturer _capturer;
    private readonly IOpusSink _sink;
    private readonly FloatToInt16Framer _framer;
    private readonly OpusEncoderAdapter _encoder;
    private readonly GainStage _gain = new();
    private readonly LevelMeter _level = new();
    private bool _capturing;
    private bool _sending;

    public AudioPipeline(IAudioCapturer capturer, IOpusSink sink, OpusEncoderAdapter? encoder = null)
    {
        _capturer = capturer;
        _sink = sink;
        _encoder = encoder ?? new OpusEncoderAdapter(capturer.Format.SampleRate, capturer.Format.Channels);
        _framer = new FloatToInt16Framer(_encoder.SamplesPerChannel, capturer.Format.Channels);
        _capturer.SamplesAvailable += OnSamples;
    }

    /// <summary>True while the capturer is running (meter is live).</summary>
    public bool IsRunning => _capturing;
    public bool IsCapturing => _capturing;
    /// <summary>True while Opus frames are being encoded/sent to a connected client.</summary>
    public bool IsSending => _sending;

    public LevelMeter Level => _level;

    /// <summary>Volume multiplier (0 = mute, 1 = unity). Smoothly ramped to avoid pops.</summary>
    public double Volume
    {
        get => _gain.Volume;
        set => _gain.Volume = (float)value;
    }

    /// <summary>Capture + send together (convenience for relay-only lifecycles).</summary>
    public void Start() { StartCapture(); _sending = true; }

    public void Stop() { _sending = false; StopCapture(); }

    /// <summary>Begin capturing for the level/waveform meter (always-on).</summary>
    public void StartCapture()
    {
        if (_capturing) return;
        _capturing = true;
        _gain.Reset(); // fade in from silence to avoid a startup pop
        _capturer.Start();
    }

    public void StopCapture()
    {
        if (!_capturing) return;
        _capturing = false;
        _sending = false;
        _capturer.Stop();
    }

    /// <summary>Gate Opus encode/send on client connection (capture keeps running for the meter).</summary>
    public void StartSending() => _sending = true;
    public void StopSending() => _sending = false;

    private void OnSamples(object? sender, ArraySegment<float> samples)
    {
        if (!_capturing) return;
        _level.Update(samples.AsSpan()); // level always tracks playback while capturing
        if (!_sending) return;

        _gain.Apply(samples.AsSpan()); // in-place; capturer supplies a fresh buffer per chunk
        _framer.Feed(samples.AsSpan(), frame =>
        {
            byte[] opus = _encoder.Encode(frame);
            _sink.Send(opus);
        });
    }

    public void Dispose()
    {
        StopCapture();
        _capturer.SamplesAvailable -= OnSamples;
    }
}
