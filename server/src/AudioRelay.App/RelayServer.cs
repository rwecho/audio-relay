using System.Diagnostics.CodeAnalysis;
using AudioRelay.Audio;
using AudioRelay.Signaling;
using AudioRelay.WebRTC;

namespace AudioRelay.App;

/// <summary>
/// Assembles the relay: WASAPI capture → framing/Opus/gain → WebRTC publisher (multi-listener),
/// with HTTP signaling over Kestrel. Capture runs always-on (driving the level/bass meters); Opus
/// encode/send is gated to only while at least one client is connected. Integration assembly.
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
        // Bind ICE to the selected adapter's IPv4 so libjuice gathers only that interface
        // (avoids hanging on IPv6). Falls back to the top-ranked candidate if none chosen.
        string? bindIp = !string.IsNullOrEmpty(settings.SelectedAdapterIp)
            ? settings.SelectedAdapterIp
            : Network.GetCandidateAddresses().FirstOrDefault()?.Ip;
        _publisher = new RtcMediaPublisher(RtcMediaConfig.DefaultCname, bindIp);
        _capturer = CreateCapturer();
        _pipeline = new AudioPipeline(_capturer, _publisher) { Volume = settings.Volume };
        _endpoint = new SignalingEndpoint(settings.Pin, _publisher, BuildStats);
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

    /// <summary>Live loudness (RMS) for the tray meter.</summary>
    public double CurrentLevel => _pipeline.Level.LatestRms;

    /// <summary>Live bass-band impact (0..1) driving the rhythm-particle effect.</summary>
    public double CurrentBass => _pipeline.Bass.LatestBass;

    /// <summary>Number of listeners currently connected.</summary>
    public int CurrentClientCount => _publisher.ConnectedCount;

    /// <summary>Per-listener telemetry (id, fps, latency) for the UI.</summary>
    public IReadOnlyList<ConnectedClient> GetClients() => _publisher.GetClients();

    public PublisherStats GetStats() => _publisher.GetStats();

    /// <summary>Object serialized by GET /stats: counters + level/bass + per-listener detail.</summary>
    private object BuildStats()
    {
        var p = _publisher.GetStats();
        return new
        {
            p.FramesSent,
            p.BytesSent,
            p.ClientConnected,
            Level = _pipeline.Level.LatestRms,
            Peak = _pipeline.Level.LatestPeak,
            Bass = _pipeline.Bass.LatestBass,
            ConnectedCount = _publisher.ConnectedCount,
            Clients = _publisher.GetClients()
        };
    }

    public void Start()
    {
        _host.Start();
        _pipeline.StartCapture(); // always-on level/bass meter, even with no client
        _log.Info($"Relay listening on :{_settings.Port} (pin {_settings.Pin})");
    }

    /// <summary>Switches to a different render device (null = follow system default).</summary>
    public void ChangeDevice(string? deviceId)
    {
        _settings.DeviceId = deviceId;
        bool wasSending = _clientConnected;

        _pipeline.Dispose();
        _capturer.DefaultDeviceChanged -= OnDefaultDeviceChanged;
        _capturer.Dispose();

        _capturer = CreateCapturer();
        _capturer.DefaultDeviceChanged += OnDefaultDeviceChanged;
        _pipeline = new AudioPipeline(_capturer, _publisher) { Volume = _settings.Volume };

        _pipeline.StartCapture();              // meter keeps running
        if (wasSending) _pipeline.StartSending();
        _log.Info(deviceId is null ? "Switched to system default device." : $"Switched to device {deviceId}.");
    }

    private WasapiLoopbackCapturer CreateCapturer() => new(_settings.DeviceId);

    private void OnDefaultDeviceChanged(object? sender, EventArgs e)
    {
        _log.Info("Default audio device changed; restarting capture.");
        _capturer.Stop();
        _capturer.Start();
    }

    private void OnClientConnection(bool connected)
    {
        _clientConnected = connected;
        if (connected) { _pipeline.StartSending(); _log.Info("Client connected."); }
        else { _pipeline.StopSending(); _log.Info("Client disconnected."); }
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
