using System.Diagnostics.CodeAnalysis;
using AudioRelay.Audio;
using AudioRelay.Signaling;
using DataChannelDotnet;
using DataChannelDotnet.Bindings;
using DataChannelDotnet.Data;
using DataChannelDotnet.Impl;

namespace AudioRelay.WebRTC;

/// <summary>One connected listener's datachannel + state.</summary>
public sealed record ConnectedClient(string Id, double ConnectedSeconds, double? LatencyMs);

/// <summary>
/// Publishes raw PCM audio to multiple simultaneous listeners over libdatachannel DATA CHANNELS
/// (unreliable, unordered — RTP-like) instead of the broken RTP/Opus media track. libdatachannel's
/// datachannel is mature; the Opus media track in DataChannelDotnet 1.3.1 has no timestamp
/// advancement API (every packet gets the same RTP timestamp → the receiver's jitter buffer never
/// emits → 0 samples decoded). Sending raw float PCM over a datachannel sidesteps RTP/Opus
/// entirely: low latency (no encode/decode), LAN-bandwidth-cheap (~1.5 Mbps stereo 48k/16-bit),
/// and decodes+plays in any client via plain Web Audio / PCM playback. The server is the SDP
/// offerer. UNVERIFIED IN UNIT TESTS — exercises native libdatachannel. Excluded from coverage.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class RtcDataChannelPublisher : ISignalingHandler, IAudioDataSink, IDisposable
{
    private sealed class Session : IDisposable
    {
        public readonly string Id = Guid.NewGuid().ToString("N")[..8];
        public readonly DateTime Created = DateTime.UtcNow;
        public IRtcPeerConnection? Peer;
        public IRtcDataChannel? Channel;
        public bool Pending = true;
        public bool Connected;
        public DateTime ConnectedSince;
        public long FramesSent;
        public double? LatencyMs;
        public void Dispose() { try { Peer?.Dispose(); } catch { } } // Peer.Dispose closes its datachannels
    }

    private static readonly TimeSpan PendingTtl = TimeSpan.FromSeconds(15);
    private readonly string? _bindAddress;
    private readonly object _gate = new();
    private readonly Dictionary<string, Session> _sessions = new();
    private readonly Queue<string> _pending = new();
    private bool _anyConnected;

    public event Action<bool>? ClientConnectionChanged;
    public bool IsClientConnected => Volatile.Read(ref _anyConnected);

    public int ConnectedCount { get { lock (_gate) return _sessions.Values.Count(s => s.Connected); } }

    public RtcDataChannelPublisher(string? bindAddress = null) => _bindAddress = bindAddress;

    public IReadOnlyList<ConnectedClient> GetClients()
    {
        var now = DateTime.UtcNow;
        var list = new List<ConnectedClient>();
        lock (_gate)
            foreach (var s in _sessions.Values)
            {
                if (!s.Connected) continue;
                double secs = s.ConnectedSince == default ? 0 : (now - s.ConnectedSince).TotalSeconds;
                list.Add(new ConnectedClient(s.Id, Math.Round(secs, 0), s.LatencyMs));
            }
        return list;
    }

    public SignalingSdp CreateOffer()
    {
        Session s;
        RtcPeerConnection peer;
        ManualResetEventSlim gathered;
        lock (_gate)
        {
            ExpireStalePending();
            s = new Session();
            var config = new RtcPeerConfiguration();
            if (!string.IsNullOrEmpty(_bindAddress)) config.BindAddress = _bindAddress;
            peer = new RtcPeerConnection(config);
            s.Peer = peer;
            // Unreliable + unordered datachannel = RTP-like semantics (drop late, no retransmit).
            s.Channel = peer.CreateDataChannel(new RtcCreateDataChannelArgs { Label = "audio", Unreliable = true, Unordered = true });
            s.Channel.OnOpen += _ => OnChannelOpen(s);
            peer.OnConnectionStateChange += (_, state) => OnSessionState(s, state);
            gathered = new ManualResetEventSlim(false);
            peer.OnGatheringStateChange += (_, g) => { if (g == rtcGatheringState.RTC_GATHERING_COMPLETE) gathered.Set(); };
            peer.SetLocalDescription(RtcDescriptionType.Offer);
            if (peer.GatheringState == rtcGatheringState.RTC_GATHERING_COMPLETE) gathered.Set();
            _sessions[s.Id] = s;
            _pending.Enqueue(s.Id);
        }
        // Wait OUTSIDE the lock (OnSessionState needs _gate; see RtcMediaPublisher deadlock note).
        if (!gathered.Wait(TimeSpan.FromSeconds(5)))
        {
            lock (_gate) _sessions.Remove(s.Id);
            s.Dispose();
            throw new SignalingException(503, "ICE gathering timed out");
        }
        string? sdp = peer.LocalDescription;
        if (string.IsNullOrEmpty(sdp))
        {
            lock (_gate) _sessions.Remove(s.Id);
            s.Dispose();
            throw new SignalingException(500, "Failed to generate local description");
        }
        return new SignalingSdp(SdpFixup.EnsureIpv4Only(sdp), "offer");
    }

    public void ApplyAnswer(string answerSdp)
    {
        Session? s = null;
        lock (_gate)
            while (_pending.Count > 0)
            {
                string id = _pending.Dequeue();
                if (_sessions.TryGetValue(id, out var cand) && cand.Pending) { s = cand; break; }
            }
        if (s is null || s.Peer is null) throw new SignalingException(409, "No pending offer");
        s.Peer.SetRemoteDescription(new RtcDescription { Sdp = answerSdp, Type = RtcDescriptionType.Answer });
        s.Pending = false;
    }

    /// <summary>Broadcasts one chunk of interleaved float PCM (as raw little-endian float bytes) to every open datachannel.</summary>
    public void Send(ReadOnlySpan<float> samples)
    {
        List<IRtcDataChannel>? open = null;
        lock (_gate)
            foreach (var s in _sessions.Values)
                if (s.Channel is { IsOpen: true }) (open ??= new()).Add(s.Channel);
        if (open is null || open.Count == 0) return;
        byte[] buf = new byte[samples.Length * sizeof(float)];
        System.Buffer.BlockCopy(samples.ToArray(), 0, buf, 0, buf.Length);
        foreach (var ch in open)
        {
            try { ch.Send(buf); } catch { }
            Interlocked.Increment(ref _totalFrames);
        }
    }

    private long _totalFrames;
    public PublisherStats GetStats() => new(Interlocked.Read(ref _totalFrames), 0, IsClientConnected);

    private void OnChannelOpen(Session s)
    {
        bool raise = false; bool val = false;
        lock (_gate)
        {
            if (_sessions.ContainsKey(s.Id))
            {
                s.Connected = true; s.ConnectedSince = DateTime.UtcNow;
                bool any = _sessions.Values.Any(x => x.Connected);
                if (any != _anyConnected) { _anyConnected = any; raise = true; val = any; }
            }
        }
        if (raise) ClientConnectionChanged?.Invoke(val);
    }

    private void OnSessionState(Session s, rtcState state)
    {
        bool gone = state == rtcState.RTC_CLOSED || state == rtcState.RTC_FAILED || state == rtcState.RTC_DISCONNECTED;
        if (!gone) return;
        bool raise = false; bool val = false;
        lock (_gate)
        {
            if (!_sessions.ContainsKey(s.Id)) return;
            s.Connected = false;
            _sessions.Remove(s.Id);
            s.Dispose();
            bool any = _sessions.Values.Any(x => x.Connected);
            if (any != _anyConnected) { _anyConnected = any; raise = true; val = any; }
        }
        if (raise) ClientConnectionChanged?.Invoke(val);
    }

    private void ExpireStalePending()
    {
        if (_sessions.Count == 0) return;
        foreach (var s in _sessions.Values.Where(x => x.Pending && DateTime.UtcNow - x.Created > PendingTtl).ToList())
        { _sessions.Remove(s.Id); s.Dispose(); }
    }

    public void Dispose()
    {
        lock (_gate) { foreach (var s in _sessions.Values) s.Dispose(); _sessions.Clear(); _pending.Clear(); _anyConnected = false; }
    }
}
