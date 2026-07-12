namespace AudioRelay.WebRTC;

/// <summary>Cumulative publish counters surfaced to the stats panel.</summary>
public sealed record PublisherStats(long FramesSent, long BytesSent, bool ClientConnected);
