namespace AudioRelay.Audio;

/// <summary>Consumes encoded Opus frames produced by the audio pipeline (the WebRTC layer).</summary>
public interface IOpusSink
{
    void Send(ReadOnlySpan<byte> opusFrame);
}
