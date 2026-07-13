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
}
