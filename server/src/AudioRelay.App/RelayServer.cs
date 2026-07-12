using System.Diagnostics.CodeAnalysis;
using AudioRelay.Audio;
using AudioRelay.Signaling;
using AudioRelay.WebRTC;

namespace AudioRelay.App;

/// <summary>
/// Assembles the relay: WASAPI capture → framing/Opus/gain → WebRTC publisher, with HTTP
/// signaling over Kestrel. Audio runs only while a client is connected. Integration assembly;
/// excluded from coverage.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class RelayServer : IDisposable
{
    private readonly Settings _settings;
    private readonly ILogger _log;
    private readonly RtcMediaPublisher _publisher;
    private readonly SignalingEndpoint _endpoint;
    private readonly SignalingKestrelHost _host;
    private WasapiLoopbackCapturer _capturer;
    private AudioPipeline _pipeline;
    private bool _clientConnected;

    public RelayServer(Settings settings, ILogger logger)
    {
        _settings = settings;
        _log = logger;
        _publisher = new RtcMediaPublisher();
        _capturer = CreateCapturer();
        _pipeline = new AudioPipeline(_capturer, _publisher) { Volume = settings.Volume };
        _endpoint = new SignalingEndpoint(settings.Pin, _publisher);
        _host = new SignalingKestrelHost($"http://0.0.0.0:{settings.Port}", _endpoint);

        _publisher.ClientConnectionChanged += OnClientConnection;
        _capturer.DefaultDeviceChanged += OnDefaultDeviceChanged;
    }

    public event Action<bool>? ClientConnectionChanged
    {
        add => _publisher.ClientConnectionChanged += value;
        remove => _publisher.ClientConnectionChanged -= value;
    }

    public double Volume
    {
        get => _pipeline.Volume;
        set => _pipeline.Volume = value;
    }

    public PublisherStats GetStats() => _publisher.GetStats();

    public void Start()
    {
        _host.Start();
        _log.Info($"Relay listening on :{_settings.Port} (pin {_settings.Pin})");
    }

    /// <summary>Switches to a different render device (null = follow system default).</summary>
    public void ChangeDevice(string? deviceId)
    {
        _settings.DeviceId = deviceId;
        bool wasRunning = _pipeline.IsRunning && _clientConnected;

        _pipeline.Dispose();
        _capturer.DefaultDeviceChanged -= OnDefaultDeviceChanged;
        _capturer.Dispose();

        _capturer = CreateCapturer();
        _capturer.DefaultDeviceChanged += OnDefaultDeviceChanged;
        _pipeline = new AudioPipeline(_capturer, _publisher) { Volume = _settings.Volume };

        if (wasRunning) _pipeline.Start();
        _log.Info(deviceId is null ? "Switched to system default device." : $"Switched to device {deviceId}.");
    }

    private WasapiLoopbackCapturer CreateCapturer() => new(_settings.DeviceId);

    private void OnDefaultDeviceChanged(object? sender, EventArgs e)
    {
        // Same capturer object; restart inner capture on the new default, pipeline keeps its subscription.
        _log.Info("Default audio device changed; restarting capture.");
        _capturer.Stop();
        _capturer.Start();
    }

    private void OnClientConnection(bool connected)
    {
        _clientConnected = connected;
        if (connected) { _pipeline.Start(); _log.Info("Client connected."); }
        else { _pipeline.Stop(); _log.Info("Client disconnected."); }
    }

    public void Dispose()
    {
        _publisher.ClientConnectionChanged -= OnClientConnection;
        _capturer.DefaultDeviceChanged -= OnDefaultDeviceChanged;
        _pipeline.Dispose();
        _capturer.Dispose();
        _publisher.Dispose();
        _host.Dispose();
    }
}
