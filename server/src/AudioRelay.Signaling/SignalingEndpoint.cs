using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioRelay.Signaling;

/// <summary>
/// Pure signaling request handler: validates method/path/JSON/PIN and dispatches to
/// the WebRTC handler, returning an HTTP status + JSON body. Transport (Kestrel) lives
/// in the host app and delegates here, so this logic is fully unit-testable.
/// </summary>
public sealed class SignalingEndpoint
{
    private const string OfferPath = "/offer";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _expectedPin;
    private readonly ISignalingHandler _handler;

    public SignalingEndpoint(string expectedPin, ISignalingHandler handler)
    {
        _expectedPin = expectedPin ?? throw new ArgumentNullException(nameof(expectedPin));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>Returns the HTTP status code and response body for a signaling request.</summary>
    public (int statusCode, string body) HandleRequest(string method, string path, string? bodyJson)
    {
        if (path != OfferPath)
            return (404, ErrorBody("Not found"));
        if (method != "POST")
            return (405, ErrorBody("Method not allowed"));

        SignalingOffer? offer;
        try
        {
            offer = string.IsNullOrWhiteSpace(bodyJson)
                ? null
                : JsonSerializer.Deserialize<SignalingOffer>(bodyJson);
        }
        catch (JsonException)
        {
            return (400, ErrorBody("Malformed JSON body"));
        }

        if (offer is null || string.IsNullOrWhiteSpace(offer.Sdp) || offer.Type != "offer")
            return (400, ErrorBody("Expected { \"sdp\": \"...\", \"type\": \"offer\", \"pin\": \"...\" }"));

        if (!string.Equals(offer.Pin, _expectedPin, StringComparison.Ordinal))
            return (403, ErrorBody("Invalid PIN"));

        SignalingAnswer answer;
        try
        {
            answer = _handler.HandleOffer(offer);
        }
        catch (SignalingException ex)
        {
            return (ex.StatusCode, ErrorBody(ex.Message));
        }
        catch
        {
            return (500, ErrorBody("Internal signaling error"));
        }

        return (200, JsonSerializer.Serialize(answer, JsonOptions));
    }

    private static string ErrorBody(string message) => JsonSerializer.Serialize(new { error = message });
}
