using AudioRelay.App;

namespace AudioRelay.App.Tests;

public class NetworkTests
{
    [Theory]
    [InlineData("vEthernet (Ethernet)")]
    [InlineData("vEthernet (Default Switch)")]
    [InlineData("Loopback Pseudo-Interface 1")]
    [InlineData("DockerNAT")]
    [InlineData("WSL")]
    [InlineData("docker0")]
    public void VirtualAdapterNames_AreFlagged(string name)
        => Assert.True(Network.IsVirtualAdapterName(name));

    [Theory]
    [InlineData("Wi-Fi")]
    [InlineData("Ethernet")]
    [InlineData("以太网")]
    [InlineData("Ethernet 2")]
    public void PhysicalAdapterNames_AreNotFlagged(string name)
        => Assert.False(Network.IsVirtualAdapterName(name));

    [Fact]
    public void EmptyName_IsNotFlagged()
        => Assert.False(Network.IsVirtualAdapterName(""));
}
