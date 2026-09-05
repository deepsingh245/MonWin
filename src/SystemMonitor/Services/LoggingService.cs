using System.IO;
using System.Text;

namespace SystemMonitor.Services;

/// <summary>
/// Minimal thread-safe append-only file logger. No external logging framework — this
/// app writes at most a few lines per run (startup/shutdown/failures), so a hand-rolled
/// writer with simple size-based rollover is enough. Never logs personal information,
/// file contents, or credentials — only short operational messages.
/// </summary>
public sealed class LoggingService : ILoggingService
{
    private const long MaxLogSizeBytes = 1 * 1024 * 1024; // 1 MB
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public LoggingService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SystemMonitor", "logs");
        Directory.CreateDirectory(dir);
        _logFilePath = Path.Combine(dir, "app.log");
    }

    public void LogInfo(string message) => Write("INFO", message);

    public void LogWarning(string message) => Write("WARN", message);

    public void LogError(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message} :: {Describe(exception)}");

    private static string Describe(Exception exception)
    {
        var inner = exception.InnerException is { } ex ? $" | Inner: {ex.GetType().Name}: {ex.Message}" : string.Empty;
        return $"{exception.GetType().Name}: {exception.Message}{inner}{Environment.NewLine}{exception.StackTrace}";
    }

    private void Write(string level, string message)
    {
        try
        {
            lock (_lock)
            {
                RollOverIfNeeded();
                var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(_logFilePath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    private void RollOverIfNeeded()
    {
        var info = new FileInfo(_logFilePath);
        if (info.Exists && info.Length > MaxLogSizeBytes)
        {
            var backupPath = Path.ChangeExtension(_logFilePath, ".old.log");
            File.Copy(_logFilePath, backupPath, overwrite: true);
            File.WriteAllText(_logFilePath, string.Empty);
        }
    }
}
