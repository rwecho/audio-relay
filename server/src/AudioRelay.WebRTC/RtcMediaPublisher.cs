using System.Diagnostics.CodeAnalysis;
using AudioRelay.Audio;
using AudioRelay.Signaling;
using DataChannelDotnet;
using DataChannelDotnet.Bindings;
using DataChannelDotnet.Data;
using DataChannelDotnet.Impl;

namespace AudioRelay.WebRTC;

/// <summary>
/// Owns the libdatachannel PeerConnection and the published Opus audio track. The server is
/// the SDP <b>offerer</b> and audio sender (libdatachannel's supported media role; see the
/// libdatachannel media-sender example). Implements <see cref="ISignalingHandler"/>
/// (CreateOffer / ApplyAnswer) and <see cref="IOpusSink"/> (writes Opus frames onto the track;
/// libdatachannel packetizes them into RTP with SR reports).
/// </summary>
/// <remarks>
/// UNVERIFIED IN UNIT TESTS — exercises native libdatachannel. Excluded from coverage.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed class RtcMediaPublisher : IOpusSink, ISignalingHandler, IDisposable
{
    private readonly uint _ssrc;
    private readonly string _cname;
    private readonly object _gate = new();
    private IRtcPeerConnection? _peer;
    private IRtcTrack? _track;

    /// <summary>Raised with true when a client connects, false when it disconnects.</summary>
    public event Action<bool>? ClientConnectionChanged;

    public bool IsClientConnected { get; private set; }

    public RtcMediaPublisher(string cname = RtcMediaConfig.DefaultCname)
    {
        _ssrc = (uint)Random.Shared.Next(1, int.MaxValue);
        _cname = cname;
    }

    /// <summary>Creates a fresh PeerConnection with a SendOnly Opus track and returns its offer SDP.</summary>
    public SignalingSdp CreateOffer()
    {
        lock (_gate)
        {
            TearDown();

            var peer = new RtcPeerConnection(new RtcPeerConfiguration());
            _peer = peer;

            peer.OnConnectionStateChange += (_, state) =>
            {
                bool connected = state == rtcState.RTC_CONNECTED;
                bool gone = state == rtcState.RTC_CLOSED ||
                            state == rtcState.RTC_FAILED ||
                            state == rtcState.RTC_DISCONNECTED;
                if (connected && !IsClientConnected)
                {
                    IsClientConnected = true;
                    ClientConnectionChanged?.Invoke(true);
                }
                else if (gone && IsClientConnected)
                {
                    IsClientConnected = false;
                    ClientConnectionChanged?.Invoke(false);
                }
            };

            // Publish the Opus audio track: SendOnly, libdatachannel packetizes + emits SR/NACK.
            var track = peer.CreateTrack(RtcMediaConfig.CreateTrackArgs(_ssrc));
            track.AddOpusPacketizer(RtcMediaConfig.CreatePacketizerArgs(_ssrc, _cname));
            track.AddRtcpSrReporter();
            track.AddRtcpNackResponder(maxPackets: 0);
            _track = track;

            // Generate the offer and wait for ICE gathering to embed host candidates (LAN).
            // Subscribe BEFORE SetLocalDescription: on LAN, host candidates can gather in
            // milliseconds, so subscribing after risks missing the COMPLETE event (intermittent
            // "ICE gathering timed out").
            var gathered = new ManualResetEventSlim(false);
            peer.OnGatheringStateChange += (_, g) =>
            {
                if (g == rtcGatheringState.RTC_GATHERING_COMPLETE) gathered.Set();
            };

            peer.SetLocalDescription(RtcDescriptionType.Offer);
            if (peer.GatheringState == rtcGatheringState.RTC_GATHERING_COMPLETE)
                gathered.Set(); // defensive: completed synchronously

            if (!gathered.Wait(TimeSpan.FromSeconds(5)))
                throw new SignalingException(503, "ICE gathering timed out");

            string? sdp = peer.LocalDescription;
            if (string.IsNullOrEmpty(sdp))
                throw new SignalingException(500, "Failed to generate local description");

            // Browsers reject libdatachannel's bare "a=ssrc:NNN"; append the cname attribute.
            string fixedSdp = SdpFixup.EnsureSsrcCname(sdp, _cname);
            return new SignalingSdp(fixedSdp, "offer");
        }
    }

    /// <summary>Applies the client's answer SDP to complete negotiation.</summary>
    public void ApplyAnswer(string answerSdp)
    {
        lock (_gate)
        {
            if (_peer is null)
                throw new SignalingException(409, "No pending offer");
            _peer.SetRemoteDescription(new RtcDescription { Sdp = answerSdp, Type = RtcDescriptionType.Answer });
        }
    }

    /// <summary>Writes one Opus frame onto the track (no-op until a client is connected).</summary>
    public void Send(ReadOnlySpan<byte> opusFrame)
    {
        IRtcTrack? track = _track;
        if (track is { IsOpen: true })
        {
            track.Write(opusFrame);
            Interlocked.Increment(ref _framesSent);
            Interlocked.Add(ref _bytesSent, opusFrame.Length);
        }
    }

    private long _framesSent;
    private long _bytesSent;

    /// <summary>Cumulative counters for the stats panel.</summary>
    public PublisherStats GetStats() => new(_framesSent, _bytesSent, IsClientConnected);

    private void TearDown()
    {
        _track?.Dispose();
        _peer?.Dispose();
        _track = null;
        _peer = null;
        IsClientConnected = false;
    }

    public void Dispose() { lock (_gate) TearDown(); }
}
