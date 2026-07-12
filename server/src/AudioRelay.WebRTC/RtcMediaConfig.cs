using DataChannelDotnet.Bindings;
using DataChannelDotnet.Data;

namespace AudioRelay.WebRTC;

/// <summary>
/// Builds the libdatachannel configuration for the published Opus audio track.
/// Pure (no native calls) so it can be unit-tested: Opus codec, payload type 111,
/// 48kHz clock, SendOnly direction (server is the audio publisher).
/// </summary>
public static class RtcMediaConfig
{
    public const byte OpusPayloadType = 111;
    public const uint OpusClockRate = 48000;
    public const string DefaultCname = "audio-relay";
    public const string DefaultMid = "0";

    public static RtcCreateTrackArgs CreateTrackArgs(uint ssrc, string trackIdPrefix = "audio-relay") => new()
    {
        Direction = rtcDirection.RTC_DIRECTION_SENDONLY,
        Codec = rtcCodec.RTC_CODEC_OPUS,
        PayloadType = OpusPayloadType,
        Ssrc = ssrc,
        Mid = DefaultMid,
        TrackId = $"{trackIdPrefix}-{ssrc}"
    };

    public static RtcPacketizerInitArgs CreatePacketizerArgs(uint ssrc, string cname = DefaultCname) => new()
    {
        Ssrc = (int)ssrc,
        Cname = cname,
        PayloadType = OpusPayloadType,
        Clockrate = OpusClockRate
    };
}
