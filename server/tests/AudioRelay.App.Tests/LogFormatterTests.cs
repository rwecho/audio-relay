using AudioRelay.App;

namespace AudioRelay.App.Tests;

public class LogFormatterTests
{
    [Fact]
    public void Format_IncludesTimestampLevelAndMessage()
    {
        var ts = new DateTime(2026, 7, 13, 14, 5, 6, 123);

        var line = LogFormatter.Format(ts, "INFO", "client connected", null);

        Assert.Contains("2026-07-13 14:05:06.123", line);
        Assert.Contains("[INFO]", line);
        Assert.Contains("client connected", line);
    }

    [Fact]
    public void Format_AppendsExceptionTypeAndMessage()
    {
        var line = LogFormatter.Format(DateTime.Now, "ERR", "boom", new InvalidOperationException("nope"));

        Assert.Contains("[ERR]", line);
        Assert.Contains("InvalidOperationException", line);
        Assert.Contains("nope", line);
    }

    [Fact]
    public void Format_OmitsExceptionPart_WhenNull()
    {
        var line = LogFormatter.Format(DateTime.Now, "INFO", "ok", null);

        Assert.DoesNotContain("|", line);
    }
}
