using System.Diagnostics.CodeAnalysis;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace AudioRelay.App;

[ExcludeFromCodeCoverage]
public partial class App : Application
{
    private RelayServer? _server;
    private Settings _settings = null!;
    private string _settingsPath = null!;
    private FileLogger _logger = null!;
    private DispatcherTimer? _statsTimer;
    private DispatcherTimer? _waveformTimer;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AudioRelay");
        Directory.CreateDirectory(appData);
        _settingsPath = Path.Combine(appData, "settings.json");
        _settings = SettingsStore.Load(_settingsPath);
        _logger = new FileLogger(Path.Combine(appData, "audio-relay.log"));

        if (_settings.AutoStart)
        {
            try { AutoStart.SetEnabled(true, Environment.ProcessPath ?? ""); }
            catch (Exception ex) { _logger.Warn($"Could not apply auto-start: {ex.Message}"); }
        }

        var devices = AudioDevices.ListRender();
        var adapters = Network.GetCandidateAddresses();
        var selectedAdapter = adapters.FirstOrDefault(a => a.Ip == _settings.SelectedAdapterIp)
                              ?? adapters.FirstOrDefault();
        string initialIp = selectedAdapter?.Ip ?? "127.0.0.1";

        var vm = new MainWindowViewModel
        {
            Adapters = adapters,
            SelectedAdapter = selectedAdapter,
            QrPayload = QrPayload.Build($"http://{initialIp}:{_settings.Port}", _settings.Pin),
            Volume = _settings.Volume,
            Devices = devices,
            SelectedDevice = _settings.DeviceId is null ? null : devices.FirstOrDefault(d => d.Id == _settings.DeviceId),
            AutoStart = _settings.AutoStart,
        };

        _server = new RelayServer(_settings, _logger);
        _server.ClientConnectionChanged += connected =>
            Dispatcher.UIThread.Post(() => vm.ClientConnected = connected);

        vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.SelectedAdapter):
                    _settings.SelectedAdapterIp = vm.SelectedAdapter?.Ip;
                    vm.QrPayload = QrPayload.Build(AdapterUrl(vm), _settings.Pin);
                    SaveSettings();
                    break;
                case nameof(MainWindowViewModel.Volume):
                    _settings.Volume = vm.Volume;
                    _server.Volume = vm.Volume;
                    SaveSettings();
                    break;
                case nameof(MainWindowViewModel.SelectedDevice):
                    _server.ChangeDevice(vm.SelectedDevice?.Id);
                    SaveSettings();
                    break;
                case nameof(MainWindowViewModel.AutoStart):
                    _settings.AutoStart = vm.AutoStart;
                    try { AutoStart.SetEnabled(vm.AutoStart, Environment.ProcessPath ?? ""); }
                    catch (Exception ex) { _logger.Warn($"Auto-start change failed: {ex.Message}"); }
                    SaveSettings();
                    break;
            }
        };

        try { _server.Start(); }
        catch (Exception ex) { vm.Status = $"启动失败: {ex.Message}"; _logger.Error("Failed to start relay", ex); }

        _statsTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, (_, _) =>
        {
            if (_server is { } s)
            {
                var st = s.GetStats();
                vm.StatsSummary = st.ClientConnected
                    ? $"已发 {st.FramesSent} 帧 · {st.BytesSent / 1024} KB"
                    : "空闲";
            }
        });
        _statsTimer.Start();

        // ~30Hz waveform: cheap (reads volatile scalars + a short locked snapshot from the capture thread).
        _waveformTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Normal, (_, _) =>
        {
            if (_server is { } s)
            {
                vm.Level = s.CurrentLevel;
                vm.Bass = s.CurrentBass;
            }
        });
        _waveformTimer.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow { DataContext = vm };

        base.OnFrameworkInitializationCompleted();

        string AdapterUrl(MainWindowViewModel v) => $"http://{v.SelectedAdapter?.Ip ?? "127.0.0.1"}:{_settings.Port}";

        void SaveSettings()
        {
            try { SettingsStore.Save(_settings, _settingsPath); }
            catch (Exception ex) { _logger.Warn($"Settings save failed: {ex.Message}"); }
        }
    }
}
