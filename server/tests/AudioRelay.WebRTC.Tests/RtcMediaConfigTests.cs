using AudioRelay.WebRTC;
using DataChannelDotnet.Bindings;
using DataChannelDotnet.Data;

namespace AudioRelay.WebRTC.Tests;

public class RtcMediaConfigTests
{
    [Fact]
    public void TrackArgs_AreOpus_SendOnly_Payload111_WithSsrc()
    {
        var args = RtcMediaConfig.CreateTrackArgs(ssrc: 0xABCDEF01u);

        Assert.Equal(rtcCodec.RTC_CODEC_OPUS, args.Codec);
        Assert.Equal(rtcDirection.RTC_DIRECTION_SENDONLY, args.Direction);
        Assert.Equal(111, args.PayloadType);
        Assert.Equal(0xABCDEF01u, args.Ssrc);
        Assert.Equal(RtcMediaConfig.DefaultMid, args.Mid);
        Assert.False(string.IsNullOrEmpty(args.TrackId));
        Assert.StartsWith("audio-relay-", args.TrackId);
    }

    [Fact]
    public void PacketizerArgs_MatchTrack_Payload111_48k_Ssrc()
    {
        var args = RtcMediaConfig.CreatePacketizerArgs(ssrc: 42u, cname: "bob");

        Assert.Equal(111, args.PayloadType);
        Assert.Equal(48000u, args.Clockrate);
        Assert.Equal(42, args.Ssrc); // Ssrc is int on the packetizer args
        Assert.Equal("bob", args.Cname);
    }

    [Fact]
    public void Constants_AreRfc7587CompliantForOpus()
    {
        Assert.Equal(111, RtcMediaConfig.OpusPayloadType);
        Assert.Equal(48000u, RtcMediaConfig.OpusClockRate);
    }
}
