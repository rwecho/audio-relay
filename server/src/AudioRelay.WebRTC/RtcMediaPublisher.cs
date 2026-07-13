using System.Diagnostics.CodeAnalysis;
using AudioRelay.Audio;
using AudioRelay.Signaling;
using DataChannelDotnet;
using DataChannelDotnet.Bindings;
using DataChannelDotnet.Data;
using DataChannelDotnet.Impl;

namespace AudioRelay.WebRTC;

/// <summary>
/// One connected listener's telemetry, surfaced to the server UI and GET /stats.
/// Latency is populated once a round-trip probe is implemented; until then it stays null.
/// </summary>
public sealed record ConnectedClient(string Id, double Fps, double ConnectedSeconds, double? LatencyMs);

/// <summary>
/// Owns libdatachannel PeerConnections for the published Opus audio track and supports MULTIPLE
/// simultaneous listeners. Each GET /offer creates an independent session (peer + SendOnly Opus
/// track) without tearing down existing ones; <see cref="Send"/> broadcasts each Opus frame to
/// every connected listener. Answers are matched to their offer in FIFO order (the signaling
/// contract is unchanged — no client-side session id needed). The server is the SDP offerer.
/// UNVERIFIED IN UNIT TESTS — exercises native libdatachannel. Excluded from coverage.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class RtcMediaPublisher : IOpusSink, ISignalingHandler, IDisposable
{
    /// <summary>One listener: its own peer, track, and connection state.</summary>
    private sealed class Session : IDisposable
    {
        public readonly string Id = Guid.NewGuid().ToString("N")[..8];
        public readonly uint Ssrc;
        public readonly DateTime Created = DateTime.UtcNow;
        public IRtcPeerConnection? Peer;
        public IRtcTrack? Track;
        public bool Pending = true; // offered, awaiting answer/connection
        public bool Connected;
        public DateTime ConnectedSince;
        public long FramesSent; // Opus frames written to this listener
        public double? LatencyMs;
        public Session(uint ssrc) => Ssrc = ssrc;
        public void Dispose() { try { Track?.Dispose(); } catch { } try { Peer?.Dispose(); } catch { } }
    }

    private static readonly TimeSpan PendingTtl = TimeSpan.FromSeconds(15);

    private readonly string _cname;
    private readonly object _gate = new();
    private readonly Dictionary<string, Session> _sessions = new();
    private readonly Queue<string> _pending = new(); // FIFO of pending session ids awaiting answer
    private long _framesSent;
    private long _bytesSent;
    private bool _anyConnected;

    /// <summary>Raised with true when the first listener connects, false when the last disconnects.</summary>
    public event Action<bool>? ClientConnectionChanged;

    public bool IsClientConnected => Volatile.Read(ref _anyConnected);

    public int ConnectedCount
    {
        get { lock (_gate) { return _sessions.Values.Count(s => s.Connected); } }
    }

    public RtcMediaPublisher(string cname = RtcMediaConfig.DefaultCname) => _cname = cname;

    /// <summary>Snapshot of every connected listener for the UI / /stats.</summary>
    public IReadOnlyList<ConnectedClient> GetClients()
    {
        var now = DateTime.UtcNow;
        List<ConnectedClient> list = new();
        lock (_gate)
        {
            foreach (var s in _sessions.Values)
            {
                if (!s.Connected) continue;
                double secs = s.ConnectedSince == default ? 0 : (now - s.ConnectedSince).TotalSeconds;
                double fps = secs > 0.5 ? s.FramesSent / secs : 0;
                list.Add(new ConnectedClient(s.Id, Math.Round(fps, 1), Math.Round(secs, 0), s.LatencyMs));
            }
        }
        return list;
    }

    /// <summary>Creates a fresh PeerConnection + SendOnly Opus track for a NEW listener, returns its offer.</summary>
    public SignalingSdp CreateOffer()
    {
        lock (_gate)
        {
            ExpireStalePending();
            var s = new Session((uint)Random.Shared.Next(1, int.MaxValue));

            var peer = new RtcPeerConnection(new RtcPeerConfiguration());
            s.Peer = peer;
            peer.OnConnectionStateChange += (_, state) => OnSessionState(s, state);

            var track = peer.CreateTrack(RtcMediaConfig.CreateTrackArgs(s.Ssrc));
            track.AddOpusPacketizer(RtcMediaConfig.CreatePacketizerArgs(s.Ssrc, _cname));
            track.AddRtcpSrReporter();
            track.AddRtcpNackResponder(maxPackets: 0);
            s.Track = track;

            // Generate the offer and wait for ICE gathering to embed host candidates (LAN).
            var gathered = new ManualResetEventSlim(false);
            peer.OnGatheringStateChange += (_, g) =>
            {
                if (g == rtcGatheringState.RTC_GATHERING_COMPLETE) gathered.Set();
            };
            peer.SetLocalDescription(RtcDescriptionType.Offer);
            if (peer.GatheringState == rtcGatheringState.RTC_GATHERING_COMPLETE)
                gathered.Set();

            if (!gathered.Wait(TimeSpan.FromSeconds(5)))
            {
                s.Dispose();
                throw new SignalingException(503, "ICE gathering timed out");
            }
            string? sdp = peer.LocalDescription;
            if (string.IsNullOrEmpty(sdp))
            {
                s.Dispose();
                throw new SignalingException(500, "Failed to generate local description");
            }

            _sessions[s.Id] = s;
            _pending.Enqueue(s.Id);
            string fixedSdp = SdpFixup.EnsureSsrcCname(sdp, _cname);
            fixedSdp = SdpFixup.EnsureIpv4Only(fixedSdp); // IPv4-only: avoid broken IPv6 pairs (libdatachannel #1006)
            return new SignalingSdp(fixedSdp, "offer");
        }
    }

    /// <summary>Applies the client's answer to the oldest pending session (FIFO).</summary>
    public void ApplyAnswer(string answerSdp)
    {
        Session? s = null;
        lock (_gate)
        {
            while (_pending.Count > 0)
            {
                string id = _pending.Dequeue();
                if (_sessions.TryGetValue(id, out var cand) && cand.Pending) { s = cand; break; }
            }
        }
        if (s is null || s.Peer is null)
            throw new SignalingException(409, "No pending offer");
        s.Peer.SetRemoteDescription(new RtcDescription { Sdp = answerSdp, Type = RtcDescriptionType.Answer });
        s.Pending = false;
    }

    /// <summary>Broadcasts one Opus frame to every connected listener's open track.</summary>
    public void Send(ReadOnlySpan<byte> opusFrame)
    {
        List<Session>? targets = null;
        lock (_gate)
        {
            foreach (var s in _sessions.Values)
                if (s.Track is { IsOpen: true })
                    (targets ??= new List<Session>()).Add(s);
        }
        if (targets is null || targets.Count == 0) return;
        foreach (var s in targets)
        {
            s.Track!.Write(opusFrame);
            Interlocked.Increment(ref s.FramesSent);
        }
        Interlocked.Add(ref _framesSent, targets.Count);
        Interlocked.Add(ref _bytesSent, opusFrame.Length * targets.Count);
    }

    /// <summary>Cumulative counters across all listeners (for the stats panel / /stats).</summary>
    public PublisherStats GetStats()
        => new(Interlocked.Read(ref _framesSent), Interlocked.Read(ref _bytesSent), IsClientConnected);

    private void OnSessionState(Session s, rtcState state)
    {
        bool connected = state == rtcState.RTC_CONNECTED;
        bool gone = state == rtcState.RTC_CLOSED ||
                    state == rtcState.RTC_FAILED ||
                    state == rtcState.RTC_DISCONNECTED;

        bool raise = false;
        bool newValue = false;
        lock (_gate)
        {
            if (!_sessions.ContainsKey(s.Id)) return; // already cleaned up
            if (connected && !s.Connected)
            {
                s.Connected = true;
                s.ConnectedSince = DateTime.UtcNow;
            }
            else if (gone)
            {
                s.Connected = false;
            }

            bool any = false;
            foreach (var x in _sessions.Values)
                if (x.Connected) { any = true; break; }

            if (any != _anyConnected)
            {
                _anyConnected = any;
                raise = true;
                newValue = any;
            }

            if (gone)
            {
                _sessions.Remove(s.Id);
                s.Dispose();
            }
        }
        if (raise) ClientConnectionChanged?.Invoke(newValue);
    }

    /// <summary>Drops pending sessions that never received an answer (called under lock).</summary>
    private void ExpireStalePending()
    {
        if (_sessions.Count == 0) return;
        var stale = _sessions.Values
            .Where(x => x.Pending && DateTime.UtcNow - x.Created > PendingTtl)
            .ToList();
        foreach (var x in stale)
        {
            _sessions.Remove(x.Id);
            x.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var s in _sessions.Values) s.Dispose();
            _sessions.Clear();
            _pending.Clear();
            _anyConnected = false;
        }
    }
}
