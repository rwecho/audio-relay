using AudioRelay.App;

namespace AudioRelay.App.Tests;

public class AutoStartTests
{
    [Fact]
    public void CommandLineFor_QuotesExePath()
    {
        Assert.Equal("\"C:\\Program Files\\AudioRelay\\app.exe\"",
            AutoStart.CommandLineFor("C:\\Program Files\\AudioRelay\\app.exe"));
    }

    [Fact]
    public void CommandLineFor_HandlesSimplePath()
    {
        Assert.Equal("\"app.exe\"", AutoStart.CommandLineFor("app.exe"));
    }
}
