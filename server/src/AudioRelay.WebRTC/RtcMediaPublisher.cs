using System.Diagnostics.CodeAnalysis;
using AudioRelay.Audio;
using AudioRelay.Signaling;
using DataChannelDotnet;
using DataChannelDotnet.Bindings;
using DataChannelDotnet.Data;
using DataChannelDotnet.Impl;

namespace AudioRelay.WebRTC;

/// <summary>
/// Owns the libdatachannel PeerConnection and the published Opus audio track.
/// Implements <see cref="ISignalingHandler"/> (offer → answer) and <see cref="IOpusSink"/>
/// (writes Opus frames onto the track; libdatachannel packetizes them into RTP with
/// correct timestamps, sequence numbers and SR reports).
/// </summary>
/// <remarks>
/// UNVERIFIED IN UNIT TESTS — exercises the native libdatachannel library. Correctness of
/// the SDP/ICE/timing path is validated at E2E (browser test-tone client → phone). Excluded
/// from coverage because every method calls into native code that cannot run in a unit test.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed class RtcMediaPublisher : IOpusSink, ISignalingHandler, IDisposable
{
    private readonly uint _ssrc;
    private readonly string _cname;
    private readonly object _gate = new();
    private IRtcPeerConnection? _peer;
    private IRtcTrack? _track;
    private long _framesSent;
    private long _bytesSent;

    /// <summary>Raised with true when a client connects, false when it disconnects.</summary>
    public event Action<bool>? ClientConnectionChanged;

    public RtcMediaPublisher(string cname = RtcMediaConfig.DefaultCname)
    {
        _ssrc = (uint)Random.Shared.Next(1, int.MaxValue);
        _cname = cname;
    }

    /// <summary>True once a client is connected and audio can flow.</summary>
    public bool IsClientConnected { get; private set; }

    public SignalingAnswer HandleOffer(SignalingOffer offer)
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

            // Non-trickle: accept the offer, generate the answer, wait for ICE gathering to
            // embed host candidates in the answer (LAN only — no STUN/TURN needed).
            peer.SetRemoteDescription(new RtcDescription { Sdp = offer.Sdp, Type = RtcDescriptionType.Offer });
            peer.SetLocalDescription(RtcDescriptionType.Answer);

            var gathered = new ManualResetEventSlim(false);
            peer.OnGatheringStateChange += (_, g) =>
            {
                if (g == rtcGatheringState.RTC_GATHERING_COMPLETE)
                    gathered.Set();
            };

            if (!gathered.Wait(TimeSpan.FromSeconds(5)))
                throw new SignalingException(503, "ICE gathering timed out");

            string? sdp = peer.LocalDescription;
            if (string.IsNullOrEmpty(sdp))
                throw new SignalingException(500, "Failed to generate local description");

            return new SignalingAnswer(sdp, "answer");
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
