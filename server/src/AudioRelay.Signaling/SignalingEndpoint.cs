using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioRelay.Signaling;

/// <summary>
/// Pure signaling request handler. The server is the SDP offerer / audio sender:
///   POST /offer  {pin}                 → 200 {sdp, type:"offer"}   (server's offer)
///   POST /answer {sdp, type, pin}      → 200 {}                     (apply client's answer)
/// Transport (Kestrel) delegates here; logic is fully unit-testable.
/// </summary>
public sealed class SignalingEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _expectedPin;
    private readonly ISignalingHandler _handler;
    private readonly Func<object?>? _statsProvider;

    public SignalingEndpoint(string expectedPin, ISignalingHandler handler, Func<object?>? statsProvider = null)
    {
        _expectedPin = expectedPin ?? throw new ArgumentNullException(nameof(expectedPin));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _statsProvider = statsProvider;
    }

    public (int statusCode, string body) HandleRequest(string method, string path, string? bodyJson)
    {
        if (method == "GET" && path == "/stats" && _statsProvider is not null)
            return (200, JsonSerializer.Serialize(_statsProvider(), JsonOptions));

        if (path != "/offer" && path != "/answer")
            return (404, ErrorBody("Not found"));
        if (method != "POST")
            return (405, ErrorBody("Method not allowed"));

        if (path == "/offer")
        {
            var req = TryParse<SignalingOfferRequest>(bodyJson);
            if (req is null || string.IsNullOrWhiteSpace(req.Pin))
                return (400, ErrorBody("Expected { \"pin\": \"...\" }"));
            if (!string.Equals(req.Pin, _expectedPin, StringComparison.Ordinal))
                return (403, ErrorBody("Invalid PIN"));

            try
            {
                var offer = _handler.CreateOffer();
                return (200, JsonSerializer.Serialize(offer, JsonOptions));
            }
            catch (SignalingException ex) { return (ex.StatusCode, ErrorBody(ex.Message)); }
            catch (Exception ex) { Console.Error.WriteLine($"[signaling] {ex}"); return (500, ErrorBody("Internal signaling error")); }
        }
        else // /answer
        {
            var req = TryParse<SignalingAnswerRequest>(bodyJson);
            if (req is null || string.IsNullOrWhiteSpace(req.Sdp) || req.Type != "answer")
                return (400, ErrorBody("Expected { \"sdp\": \"...\", \"type\": \"answer\", \"pin\": \"...\" }"));
            if (!string.Equals(req.Pin, _expectedPin, StringComparison.Ordinal))
                return (403, ErrorBody("Invalid PIN"));

            try
            {
                _handler.ApplyAnswer(req.Sdp);
                return (200, "{}");
            }
            catch (SignalingException ex) { return (ex.StatusCode, ErrorBody(ex.Message)); }
            catch (Exception ex) { Console.Error.WriteLine($"[signaling] {ex}"); return (500, ErrorBody("Internal signaling error")); }
        }
    }

    private static T? TryParse<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return null; }
    }

    private static string ErrorBody(string message) => JsonSerializer.Serialize(new { error = message });
}
