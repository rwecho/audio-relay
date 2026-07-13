using System.Text.Json;
using AudioRelay.Signaling;

namespace AudioRelay.Signaling.Tests;

public class SignalingEndpointTests
{
    private const string Pin = "1234";
    private readonly FakeHandler _handler = new();
    private readonly SignalingEndpoint _endpoint;

    public SignalingEndpointTests() => _endpoint = new SignalingEndpoint(Pin, _handler);

    private static string Json(object o) => JsonSerializer.Serialize(o);

    [Fact]
    public void UnknownPath_Returns404()
        => Assert.Equal(404, _endpoint.HandleRequest("POST", "/nope", "").statusCode);

    [Fact]
    public void GetOnOffer_Returns405()
        => Assert.Equal(405, _endpoint.HandleRequest("GET", "/offer", "").statusCode);

    [Fact]
    public void Offer_MalformedJson_Returns400()
        => Assert.Equal(400, _endpoint.HandleRequest("POST", "/offer", "{not json").statusCode);

    [Fact]
    public void Offer_MissingPin_Returns400()
        => Assert.Equal(400, _endpoint.HandleRequest("POST", "/offer", Json(new { })).statusCode);

    [Theory]
    [InlineData("wrong")]
    [InlineData("1235")]
    public void Offer_WrongPin_Returns403(string pin)
        => Assert.Equal(403, _endpoint.HandleRequest("POST", "/offer", Json(new { pin })).statusCode);

    [Fact]
    public void Offer_CorrectPin_Returns200AndServerOffer()
    {
        _handler.OfferToReturn = new SignalingSdp("SERVER_OFFER_SDP", "offer");

        var (status, body) = _endpoint.HandleRequest("POST", "/offer", Json(new { pin = Pin }));

        Assert.Equal(200, status);
        var sdp = JsonSerializer.Deserialize<SignalingSdp>(body);
        Assert.NotNull(sdp);
        Assert.Equal("SERVER_OFFER_SDP", sdp!.Sdp);
        Assert.Equal("offer", sdp.Type);
    }

    [Fact]
    public void Answer_CorrectPinAndSdp_AppliesAnswer_Returns200()
    {
        var (status, body) = _endpoint.HandleRequest("POST", "/answer",
            Json(new { sdp = "CLIENT_ANSWER", type = "answer", pin = Pin }));

        Assert.Equal(200, status);
        Assert.Equal("CLIENT_ANSWER", _handler.LastAppliedAnswer);
    }

    [Fact]
    public void Answer_WrongPin_Returns403()
        => Assert.Equal(403,
            _endpoint.HandleRequest("POST", "/answer", Json(new { sdp = "x", type = "answer", pin = "bad" })).statusCode);

    [Fact]
    public void Answer_TypeNotAnswer_Returns400()
        => Assert.Equal(400,
            _endpoint.HandleRequest("POST", "/answer", Json(new { sdp = "x", type = "offer", pin = Pin })).statusCode);

    [Fact]
    public void Answer_MissingSdp_Returns400()
        => Assert.Equal(400,
            _endpoint.HandleRequest("POST", "/answer", Json(new { type = "answer", pin = Pin })).statusCode);

    [Fact]
    public void CreateOffer_SignalingException_ReturnsItsStatusCode()
    {
        _handler.OfferToReturn = new SignalingSdp("x", "offer");
        _handler.OfferThrow = new SignalingException(503, "busy");

        var (status, body) = _endpoint.HandleRequest("POST", "/offer", Json(new { pin = Pin }));

        Assert.Equal(503, status);
        Assert.Contains("busy", body);
    }

    [Fact]
    public void ApplyAnswer_UnexpectedException_Returns500()
    {
        _handler.AnswerThrow = new InvalidOperationException("boom");
        Assert.Equal(500,
            _endpoint.HandleRequest("POST", "/answer", Json(new { sdp = "x", type = "answer", pin = Pin })).statusCode);
    }

    private sealed class FakeHandler : ISignalingHandler
    {
        public SignalingSdp OfferToReturn { get; set; } = new("default", "offer");
        public Exception? OfferThrow { get; set; }
        public Exception? AnswerThrow { get; set; }
        public string? LastAppliedAnswer { get; private set; }

        public SignalingSdp CreateOffer()
        {
            if (OfferThrow is not null) throw OfferThrow;
            return OfferToReturn;
        }

        public void ApplyAnswer(string answerSdp)
        {
            if (AnswerThrow is not null) throw AnswerThrow;
            LastAppliedAnswer = answerSdp;
        }
    }
}
