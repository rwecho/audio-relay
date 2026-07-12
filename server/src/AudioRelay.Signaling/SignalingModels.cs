using System.Text.Json.Serialization;

namespace AudioRelay.Signaling;

public sealed record SignalingOffer(
    [property: JsonPropertyName("sdp")] string Sdp,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("pin")] string Pin);

public sealed record SignalingAnswer(
    [property: JsonPropertyName("sdp")] string Sdp,
    [property: JsonPropertyName("type")] string Type);
