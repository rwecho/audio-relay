namespace AudioRelay.Signaling;

/// <summary>
/// Server-side WebRTC negotiation (the server is the SDP offerer and audio sender).
/// Implementations own the PeerConnection lifecycle (libdatachannel).
/// </summary>
public interface ISignalingHandler
{
    /// <summary>Create the server's SDP offer (with its SendOnly Opus audio track).</summary>
    SignalingSdp CreateOffer();

    /// <summary>Apply the client's answer SDP. Throws <see cref="SignalingException"/> on failure.</summary>
    void ApplyAnswer(string answerSdp);
}
