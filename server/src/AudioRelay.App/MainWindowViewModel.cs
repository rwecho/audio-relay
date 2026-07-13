using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AudioRelay.App;

/// <summary>
/// Main-window view state (framework-agnostic INotifyPropertyChanged). All UI binding logic;
/// no Avalonia or native dependencies, so fully unit-testable.
/// </summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private string _status = "等待连接…";
    private string _clientName = "";
    private string _qrPayload = "";
    private string _statsSummary = "";
    private bool _clientConnected;
    private double _volume = 1.0;
    private bool _autoStart;
    private IReadOnlyList<AudioDevices.Device> _devices = Array.Empty<AudioDevices.Device>();
    private AudioDevices.Device? _selectedDevice;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string ClientName
    {
        get => _clientName;
        set { _clientName = value; OnPropertyChanged(); UpdateStatus(); }
    }

    public string QrPayload
    {
        get => _qrPayload;
        set { _qrPayload = value; OnPropertyChanged(); }
    }

    public string StatsSummary
    {
        get => _statsSummary;
        set { _statsSummary = value; OnPropertyChanged(); }
    }

    public bool ClientConnected
    {
        get => _clientConnected;
        set { _clientConnected = value; OnPropertyChanged(); UpdateStatus(); }
    }

    /// <summary>Server-side volume multiplier (0..1+), applied with a smooth ramp.</summary>
    public double Volume
    {
        get => _volume;
        set { _volume = value; OnPropertyChanged(); }
    }

    public bool AutoStart
    {
        get => _autoStart;
        set { _autoStart = value; OnPropertyChanged(); }
    }

    public IReadOnlyList<AudioDevices.Device> Devices
    {
        get => _devices;
        set { _devices = value; OnPropertyChanged(); }
    }

    public AudioDevices.Device? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); }
    }

    private IReadOnlyList<AdapterCandidate> _adapters = Array.Empty<AdapterCandidate>();
    private AdapterCandidate? _selectedAdapter;

    /// <summary>Pickable network adapters; the selected one drives the IP embedded in the QR payload.</summary>
    public IReadOnlyList<AdapterCandidate> Adapters
    {
        get => _adapters;
        set { _adapters = value; OnPropertyChanged(); }
    }

    public AdapterCandidate? SelectedAdapter
    {
        get => _selectedAdapter;
        set { _selectedAdapter = value; OnPropertyChanged(); }
    }

    private void UpdateStatus()
    {
        Status = _clientConnected
            ? $"已连接 · {(string.IsNullOrEmpty(_clientName) ? "客户端" : _clientName)}"
            : "等待连接…";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
