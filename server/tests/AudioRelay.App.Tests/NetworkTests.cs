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

public class NetworkRankingTests
{
    [Fact]
    public void GatewayAndPrivateLan_RanksFirst_CgnatLast()
    {
        var ethernet = new AdapterCandidate("Ethernet", "192.168.20.110", HasGateway: true);
        var wifi = new AdapterCandidate("Wi-Fi", "192.168.1.5", HasGateway: true);
        var tailscale = new AdapterCandidate("Tailscale", "100.98.227.87", HasGateway: false);

        var ranked = Network.RankCandidates(new[] { tailscale, ethernet, wifi });

        Assert.Equal("192.168.20.110", ranked[0].Ip); // gateway + private, alias "Ethernet" < "Wi-Fi"
        Assert.Equal("192.168.1.5", ranked[1].Ip);
        Assert.Equal("100.98.227.87", ranked[2].Ip); // non-gateway CGNAT last
    }

    [Fact]
    public void RankCandidates_EmptyInput_ReturnsEmpty()
        => Assert.Empty(Network.RankCandidates(Array.Empty<AdapterCandidate>()));

    [Theory]
    [InlineData("192.168.20.110", true)]
    [InlineData("10.0.0.5", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.255", true)]
    [InlineData("172.15.0.1", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("100.98.227.87", false)] // CGNAT / Tailscale — not RFC1918
    [InlineData("8.8.8.8", false)]
    [InlineData("not-an-ip", false)]
    public void IsPrivateLan_ClassifiesCorrectly(string ip, bool expected)
        => Assert.Equal(expected, Network.IsPrivateLan(ip));
}
