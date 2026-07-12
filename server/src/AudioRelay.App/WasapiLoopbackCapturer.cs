using System.Diagnostics.CodeAnalysis;
using AudioRelay.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AudioRelay.App;

/// <summary>
/// Captures Windows system audio via WASAPI loopback and delivers it as interleaved 48kHz
/// stereo float. When no specific device is given it follows the system default and raises
/// <see cref="DefaultDeviceChanged"/> so the host can restart on the new default. Non-48kHz
/// device formats are linearly resampled. Integration code; excluded from coverage.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class WasapiLoopbackCapturer : IAudioCapturer, IDisposable
{
    private const int PipelineRate = 48000;
    private const int Channels = 2;

    private readonly string? _deviceId; // null = follow system default
    private readonly MMDeviceEnumerator _enumerator = new();
    private WasapiLoopbackCapture? _capture;
    private Timer? _watcher;
    private string? _currentDefaultId;
    private int _deviceRate;

    public AudioFormat Format { get; } = new(PipelineRate, Channels);
    public event EventHandler<ArraySegment<float>>? SamplesAvailable;
    public event EventHandler? DefaultDeviceChanged;

    public WasapiLoopbackCapturer(string? deviceId = null) => _deviceId = deviceId;

    public void Start()
    {
        var device = ResolveDevice();
        if (_deviceId == null)
            _currentDefaultId = device.ID;

        _capture = new WasapiLoopbackCapture(device);
        var wf = _capture.WaveFormat;
        _deviceRate = wf.SampleRate;

        if (wf.Channels != Channels)
            throw new InvalidOperationException($"System audio must be {Channels}-channel stereo; device reports {wf.Channels}.");
        if (wf.Encoding != WaveFormatEncoding.IeeeFloat || wf.BitsPerSample != 32)
            throw new InvalidOperationException($"Expected 32-bit IEEE-float system format; got {wf.Encoding}/{wf.BitsPerSample}-bit.");

        _capture.DataAvailable += OnDataAvailable;
        _capture.StartRecording();

        if (_deviceId == null)
            _watcher = new Timer(_ => WatchDefault(), null, 2000, 2000);
    }

    private MMDevice ResolveDevice()
    {
        if (_deviceId is not null)
        {
            foreach (var d in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                if (d.ID == _deviceId) return d;
            throw new InvalidOperationException("Configured audio device was not found.");
        }
        return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

    private void WatchDefault()
    {
        try
        {
            var id = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
            if (_currentDefaultId is not null && id != _currentDefaultId)
            {
                _currentDefaultId = id;
                DefaultDeviceChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch { /* no default device available (transient) */ }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        int sampleCount = e.BytesRecorded / 4; // 32-bit float
        if (sampleCount == 0) return;

        var input = new float[sampleCount];
        Buffer.BlockCopy(e.Buffer, 0, input, 0, sampleCount * sizeof(float));

        float[] output = _deviceRate == PipelineRate
            ? input
            : LinearResampler.Resample(input, _deviceRate, PipelineRate, Channels);

        SamplesAvailable?.Invoke(this, output);
    }

    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;

        if (_capture is { } c)
        {
            c.DataAvailable -= OnDataAvailable;
            try { c.StopRecording(); } catch { /* already stopped */ }
            c.Dispose();
            _capture = null;
        }
    }

    public void Dispose() => Stop();
}
