using System.Text.Json.Serialization;

namespace AudioRelay.Signaling;

/// <summary>Request body for POST /offer (the client asks for the server's offer).</summary>
public sealed record SignalingOfferRequest(
    [property: JsonPropertyName("pin")] string Pin);

/// <summary>Request body for POST /answer (the client's answer SDP).</summary>
public sealed record SignalingAnswerRequest(
    [property: JsonPropertyName("sdp")] string Sdp,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("pin")] string Pin);

/// <summary>An SDP message returned to the client (the server's offer).</summary>
public sealed record SignalingSdp(
    [property: JsonPropertyName("sdp")] string Sdp,
    [property: JsonPropertyName("type")] string Type);
