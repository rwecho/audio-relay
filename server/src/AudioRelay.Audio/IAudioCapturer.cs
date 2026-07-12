namespace AudioRelay.Audio;

/// <summary>
/// Captures system audio and delivers it as interleaved float samples ([-1, 1]).
/// Implementations normalize their capture to <see cref="Format"/> before raising
/// <see cref="SamplesAvailable"/> (the Windows WASAPI loopback implementation
/// resamples the device mix rate down/up to the pipeline rate). The pipeline
/// therefore always receives samples at <see cref="Format"/>.
/// </summary>
public interface IAudioCapturer
{
    AudioFormat Format { get; }

    /// <summary>Raised with a chunk of normalized interleaved float samples.</summary>
    event EventHandler<ArraySegment<float>>? SamplesAvailable;

    /// <summary>Raised when the system default output device changes.</summary>
    event EventHandler? DefaultDeviceChanged;

    void Start();
    void Stop();
}
