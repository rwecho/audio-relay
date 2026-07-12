using System.Diagnostics.CodeAnalysis;
using NAudio.CoreAudioApi;

namespace AudioRelay.App;

/// <summary>Enumerates active render endpoints for the device picker. Integration code.</summary>
public static class AudioDevices
{
    public sealed record Device(string Id, string Name);

    [ExcludeFromCodeCoverage]
    public static IReadOnlyList<Device> ListRender()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => new Device(d.ID, d.FriendlyName))
            .ToList();
    }
}
