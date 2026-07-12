using System.Diagnostics.CodeAnalysis;

namespace AudioRelay.App;

public interface ILogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
}

/// <summary>Pure line formatting for the log; unit-tested independently of file IO.</summary>
public static class LogFormatter
{
    public static string Format(DateTime timestamp, string level, string message, Exception? exception)
    {
        string line = $"{timestamp:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        if (exception is not null)
            line += $" | {exception.GetType().Name}: {exception.Message}";
        return line;
    }
}

/// <summary>Appends timestamped log lines to a file. Logging must never crash the app.</summary>
[ExcludeFromCodeCoverage]
public sealed class FileLogger : ILogger
{
    private readonly string _path;
    private readonly object _gate = new();

    public FileLogger(string path)
    {
        _path = path;
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public void Info(string message) => Write("INFO", message, null);
    public void Warn(string message) => Write("WARN", message, null);
    public void Error(string message, Exception? exception = null) => Write("ERR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        string line = LogFormatter.Format(DateTime.Now, level, message, exception);
        lock (_gate)
        {
            try { File.AppendAllText(_path, line + Environment.NewLine); }
            catch { /* logging failures must not propagate */ }
        }
    }
}
