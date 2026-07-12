namespace AudioRelay.Signaling;

/// <summary>
/// Produces the WebRTC answer SDP for a verified offer. Implementations own the
/// PeerConnection lifecycle (libdatachannel). Throws <see cref="SignalingException"/>
/// to return a specific status code to the client.
/// </summary>
public interface ISignalingHandler
{
    SignalingAnswer HandleOffer(SignalingOffer offer);
}
