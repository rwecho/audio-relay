using AudioRelay.App;

namespace AudioRelay.App.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void SerializeDeserialize_RoundTripsAllFields()
    {
        var original = new Settings
        {
            Port = 9999,
            Pin = "4271",
            DeviceId = "device-abc",
            Volume = 0.75,
            AutoStart = true
        };

        var restored = SettingsStore.Deserialize(SettingsStore.Serialize(original));

        Assert.Equal(9999, restored.Port);
        Assert.Equal("4271", restored.Pin);
        Assert.Equal("device-abc", restored.DeviceId);
        Assert.Equal(0.75, restored.Volume);
        Assert.True(restored.AutoStart);
    }

    [Fact]
    public void Deserialize_MalformedJson_ReturnsDefaults()
    {
        var restored = SettingsStore.Deserialize("{ this is not json");

        Assert.Equal(8080, restored.Port);
        Assert.False(string.IsNullOrEmpty(restored.Pin)); // fresh random PIN
        Assert.False(restored.AutoStart);
    }

    [Fact]
    public void Defaults_HaveRandomPinAndStandardPort()
    {
        var d = Settings.WithDefaults();

        Assert.Equal(8080, d.Port);
        Assert.Matches(@"^\d{4}$", d.Pin);
        Assert.Equal(1.0, d.Volume);
    }
}
