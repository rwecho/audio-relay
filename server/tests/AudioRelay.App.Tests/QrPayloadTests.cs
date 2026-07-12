using AudioRelay.App;

namespace AudioRelay.App.Tests;

public class QrPayloadTests
{
    [Fact]
    public void Build_AppendsPinAsQuery()
    {
        Assert.Equal("http://192.168.1.20:8080?pin=4271",
            QrPayload.Build("http://192.168.1.20:8080", "4271"));
    }

    [Fact]
    public void Parse_RoundTripsBuild()
    {
        var payload = QrPayload.Build("http://10.0.0.5:8080", "9999");

        var (url, pin) = QrPayload.Parse(payload);

        Assert.Equal("http://10.0.0.5:8080", url);
        Assert.Equal("9999", pin);
    }
}
