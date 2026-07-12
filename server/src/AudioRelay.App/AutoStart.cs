using System.Diagnostics.CodeAnalysis;
using Microsoft.Win32;

namespace AudioRelay.App;

/// <summary>Windows current-user "Run" key auto-start. Registry IO is excluded; the command-line form is tested.</summary>
public static class AutoStart
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AudioRelay";

    /// <summary>The command line stored in the Run key (quotes the exe path).</summary>
    public static string CommandLineFor(string exePath) => $"\"{exePath}\"";

    [ExcludeFromCodeCoverage]
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is not null;
    }

    [ExcludeFromCodeCoverage]
    public static void SetEnabled(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(ValueName, CommandLineFor(exePath));
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
