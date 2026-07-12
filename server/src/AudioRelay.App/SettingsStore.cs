using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace AudioRelay.App;

/// <summary>
/// Loads/saves <see cref="Settings"/> as JSON. Pure serialize/deserialize is unit-tested;
/// file IO recovers to fresh defaults if the file is missing or unreadable (verified by test).
/// </summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Serialize(Settings settings) => JsonSerializer.Serialize(settings, Options);

    /// <summary>Deserializes settings JSON. Returns defaults on malformed JSON.</summary>
    public static Settings Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Settings>(json) ?? Settings.WithDefaults();
        }
        catch (JsonException)
        {
            return Settings.WithDefaults();
        }
    }

    [ExcludeFromCodeCoverage]
    public static Settings Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return Deserialize(File.ReadAllText(path));
        }
        catch (IOException)
        {
            // unreadable settings file -> fall back to fresh defaults
        }
        return Settings.WithDefaults();
    }

    [ExcludeFromCodeCoverage]
    public static void Save(Settings settings, string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, Serialize(settings));
    }
}
