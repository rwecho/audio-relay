using AudioRelay.App;

namespace AudioRelay.App.Tests;

public class MainWindowViewModelSettingsTests
{
    private static List<string?> CapturePropertyChanges(MainWindowViewModel vm)
    {
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }

    [Fact]
    public void Volume_Setter_HoldsValueAndRaises()
    {
        var vm = new MainWindowViewModel();
        var raised = CapturePropertyChanges(vm);

        vm.Volume = 0.5;

        Assert.Equal(0.5, vm.Volume);
        Assert.Contains(nameof(MainWindowViewModel.Volume), raised);
    }

    [Fact]
    public void AutoStart_Setter_HoldsValueAndRaises()
    {
        var vm = new MainWindowViewModel();
        var raised = CapturePropertyChanges(vm);

        vm.AutoStart = true;

        Assert.True(vm.AutoStart);
        Assert.Contains(nameof(MainWindowViewModel.AutoStart), raised);
    }

    [Fact]
    public void Devices_Setter_HoldsValueAndRaises()
    {
        var vm = new MainWindowViewModel();
        var raised = CapturePropertyChanges(vm);
        var devices = new List<AudioDevices.Device> { new("a", "Speakers"), new("b", "HDMI") };

        vm.Devices = devices;

        Assert.Same(devices, vm.Devices);
        Assert.Contains(nameof(MainWindowViewModel.Devices), raised);
    }

    [Fact]
    public void SelectedDevice_Setter_HoldsValueAndRaises()
    {
        var vm = new MainWindowViewModel();
        var raised = CapturePropertyChanges(vm);
        var device = new AudioDevices.Device("a", "Speakers");

        vm.SelectedDevice = device;

        Assert.Equal(device, vm.SelectedDevice);
        Assert.Contains(nameof(MainWindowViewModel.SelectedDevice), raised);
    }

    [Fact]
    public void Defaults_VolumeIsUnity()
    {
        var vm = new MainWindowViewModel();
        Assert.Equal(1.0, vm.Volume);
        Assert.False(vm.AutoStart);
        Assert.Empty(vm.Devices);
        Assert.Null(vm.SelectedDevice);
    }
}
