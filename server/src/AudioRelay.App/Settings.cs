namespace AudioRelay.App;

/// <summary>Persisted application settings. PIN defaults to a fresh random 4-digit value.</summary>
public sealed class Settings
{
    public int Port { get; set; } = 8080;
    public string Pin { get; set; } = "";
    public string? DeviceId { get; set; }
    public double Volume { get; set; } = 1.0;
    public bool AutoStart { get; set; }

    /// <summary>Last-selected network adapter IP for the QR payload (null = auto-pick best candidate).</summary>
    public string? SelectedAdapterIp { get; set; }

    /// <summary>Fresh settings with a randomly-generated PIN (used when no file exists yet).</summary>
    public static Settings WithDefaults() => new()
    {
        Pin = Random.Shared.Next(1000, 10000).ToString()
    };
}
