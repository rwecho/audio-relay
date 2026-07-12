using AudioRelay.App;

namespace AudioRelay.App.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void Defaults_AreDisconnected()
    {
        var vm = new MainWindowViewModel();

        Assert.False(vm.ClientConnected);
        Assert.Equal("等待连接…", vm.Status);
    }

    [Fact]
    public void ClientConnected_True_UpdatesStatusToConnected_WithClientName()
    {
        var vm = new MainWindowViewModel { ClientName = "Pixel" };

        vm.ClientConnected = true;

        Assert.StartsWith("已连接", vm.Status);
        Assert.Contains("Pixel", vm.Status);
    }

    [Fact]
    public void ClientConnected_False_UpdatesStatusToWaiting()
    {
        var vm = new MainWindowViewModel { ClientConnected = true };

        vm.ClientConnected = false;

        Assert.Equal("等待连接…", vm.Status);
    }

    [Fact]
    public void ClientName_SetAfterConnect_UpdatesStatus()
    {
        var vm = new MainWindowViewModel { ClientConnected = true };

        vm.ClientName = "iPhone";

        Assert.Contains("iPhone", vm.Status);
    }

    [Fact]
    public void ConnectedWithoutName_ShowsGenericLabel()
    {
        var vm = new MainWindowViewModel { ClientConnected = true };

        Assert.Contains("客户端", vm.Status);
    }

    [Fact]
    public void SettingQrPayload_RaisesPropertyChanged()
    {
        var vm = new MainWindowViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.QrPayload = "http://x?pin=1";

        Assert.Contains(nameof(MainWindowViewModel.QrPayload), raised);
    }

    [Fact]
    public void SettingClientConnected_RaisesPropertyChanged()
    {
        var vm = new MainWindowViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.ClientConnected = true;

        Assert.Contains(nameof(MainWindowViewModel.ClientConnected), raised);
    }
}
