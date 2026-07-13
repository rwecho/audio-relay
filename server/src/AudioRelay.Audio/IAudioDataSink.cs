namespace AudioRelay.Audio;

/// <summary>
/// Consumes raw interleaved float PCM ([-1,1]) for transport to clients. Implemented by the
/// datachannel-based publisher (raw PCM over a libdatachannel datachannel, bypassing the RTP/Opus
/// media track whose timestamp advancement is broken in DataChannelDotnet 1.3.1).
/// </summary>
public interface IAudioDataSink
{
    /// <summary>Sends one chunk of interleaved float samples to all connected listeners.</summary>
    void Send(ReadOnlySpan<float> samples);
}
