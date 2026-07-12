using System.Text.Json;
using AudioRelay.Signaling;

namespace AudioRelay.Signaling.Tests;

public class SignalingEndpointTests
{
    private const string Pin = "1234";
    private readonly FakeHandler _handler = new();
    private readonly SignalingEndpoint _endpoint;

    public SignalingEndpointTests() => _endpoint = new SignalingEndpoint(Pin, _handler);

    private static string OfferJson(string sdp = "v=0", string type = "offer", string pin = Pin) =>
        JsonSerializer.Serialize(new { sdp, type, pin });

    [Fact]
    public void UnknownPath_Returns404()
        => Assert.Equal(404, _endpoint.HandleRequest("POST", "/nope", "").statusCode);

    [Fact]
    public void GetOnOffer_Returns405()
        => Assert.Equal(405, _endpoint.HandleRequest("GET", "/offer", "").statusCode);

    [Fact]
    public void MalformedJson_Returns400()
        => Assert.Equal(400, _endpoint.HandleRequest("POST", "/offer", "{not json").statusCode);

    [Fact]
    public void EmptyBody_Returns400()
        => Assert.Equal(400, _endpoint.HandleRequest("POST", "/offer", "").statusCode);

    [Fact]
    public void MissingSdp_Returns400()
        => Assert.Equal(400,
            _endpoint.HandleRequest("POST", "/offer", JsonSerializer.Serialize(new { type = "offer", pin = Pin })).statusCode);

    [Fact]
    public void TypeNotOffer_Returns400()
        => Assert.Equal(400, _endpoint.HandleRequest("POST", "/offer", OfferJson(type: "answer")).statusCode);

    [Theory]
    [InlineData("")]
    [InlineData("wrong")]
    [InlineData("1235")]
    public void WrongPin_Returns403(string pin)
        => Assert.Equal(403, _endpoint.HandleRequest("POST", "/offer", OfferJson(pin: pin)).statusCode);

    [Fact]
    public void CorrectPin_Returns200AndAnswer()
    {
        _handler.Answer = new SignalingAnswer("ANSWER_SDP", "answer");

        var (status, body) = _endpoint.HandleRequest("POST", "/offer", OfferJson(sdp: "OFFER_SDP"));

        Assert.Equal(200, status);
        var answer = JsonSerializer.Deserialize<SignalingAnswer>(body);
        Assert.NotNull(answer);
        Assert.Equal("ANSWER_SDP", answer!.Sdp);
        Assert.Equal("answer", answer.Type);
    }

    [Fact]
    public void CorrectPin_ForwardsOfferSdpToHandler()
    {
        _handler.Answer = new SignalingAnswer("a", "answer");
        _endpoint.HandleRequest("POST", "/offer", OfferJson(sdp: "FORWARDED_SDP"));

        Assert.Equal("FORWARDED_SDP", _handler.LastReceived?.Sdp);
    }

    [Fact]
    public void HandlerSignalingException_ReturnsItsStatusCode()
    {
        _handler.Throw = new SignalingException(503, "server busy");

        var (status, body) = _endpoint.HandleRequest("POST", "/offer", OfferJson());

        Assert.Equal(503, status);
        Assert.Contains("server busy", body);
    }

    [Fact]
    public void HandlerUnexpectedException_Returns500()
    {
        _handler.Throw = new InvalidOperationException("boom");
        Assert.Equal(500, _endpoint.HandleRequest("POST", "/offer", OfferJson()).statusCode);
    }

    private sealed class FakeHandler : ISignalingHandler
    {
        public SignalingAnswer Answer { get; set; } = new("default", "answer");
        public SignalingOffer? LastReceived { get; private set; }
        public Exception? Throw { get; set; }

        public SignalingAnswer HandleOffer(SignalingOffer offer)
        {
            LastReceived = offer;
            if (Throw is not null) throw Throw;
            return Answer;
        }
    }
}
