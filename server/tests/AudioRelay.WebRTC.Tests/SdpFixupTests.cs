using AudioRelay.WebRTC;

namespace AudioRelay.WebRTC.Tests;

public class SdpFixupTests
{
    [Fact]
    public void BareSsrcLine_GetsCnameAppended()
    {
        var sdp = "m=audio 9 UDP/TLS/RTP/SAVPF 111\r\na=ssrc:1565102177\r\na=rtpmap:111 opus/48000/2\r\n";

        var fixedSdp = SdpFixup.EnsureSsrcCname(sdp, "audio-relay");

        Assert.Contains("a=ssrc:1565102177 cname:audio-relay", fixedSdp);
        Assert.DoesNotContain("a=ssrc:1565102177\r\n", fixedSdp);
    }

    [Fact]
    public void AlreadyAttributedSsrc_IsLeftAlone()
    {
        var sdp = "a=ssrc:1234 cname:already\r\n";

        var fixedSdp = SdpFixup.EnsureSsrcCname(sdp, "audio-relay");

        Assert.Equal(sdp, fixedSdp);
    }

    [Fact]
    public void NonSsrcLines_ArePreserved()
    {
        var sdp = "a=mid:0\r\na=sendonly\r\na=rtpmap:111 opus/48000/2\r\n";

        var fixedSdp = SdpFixup.EnsureSsrcCname(sdp, "audio-relay");

        Assert.Contains("a=mid:0", fixedSdp);
        Assert.Contains("a=sendonly", fixedSdp);
    }

    [Fact]
    public void EmptySdp_ReturnedUnchanged()
        => Assert.Equal("", SdpFixup.EnsureSsrcCname("", "audio-relay"));

    [Fact]
    public void EnsureIpv4Only_RemovesIpv6Candidates_KeepsIpv4()
    {
        var sdp = string.Join("\r\n",
            "m=audio 9 UDP/TLS/RTP/SAVPF 111",
            "a=candidate:1 1 UDP 2116026367 240e:3a5:5007:6ad0:7df5:1b8f:f6a4:182 53328 typ host",
            "a=candidate:4 1 UDP 2116025599 fd7a:115c:a1e0::6035:e357 53328 typ host",
            "a=candidate:2 1 UDP 2114977535 192.168.20.110 53328 typ host",
            "a=candidate:5 1 UDP 2114976767 100.98.227.87 53328 typ host",
            "a=end-of-candidates") + "\r\n";

        var fixedSdp = SdpFixup.EnsureIpv4Only(sdp);

        Assert.DoesNotContain("240e:", fixedSdp);
        Assert.DoesNotContain("fd7a:", fixedSdp);
        Assert.Contains("192.168.20.110", fixedSdp);
        Assert.Contains("100.98.227.87", fixedSdp);
        Assert.Contains("a=end-of-candidates", fixedSdp);
        Assert.Contains("m=audio 9", fixedSdp);
    }

    [Fact]
    public void EnsureIpv4Only_NoCandidateLines_LeavesSdpIntact()
    {
        var sdp = "a=mid:0\r\na=sendonly\r\na=rtpmap:111 opus/48000/2\r\n";
        Assert.Equal(sdp, SdpFixup.EnsureIpv4Only(sdp));
    }

    [Fact]
    public void EnsureIpv4Only_Empty_ReturnedUnchanged()
        => Assert.Equal("", SdpFixup.EnsureIpv4Only(""));
}
