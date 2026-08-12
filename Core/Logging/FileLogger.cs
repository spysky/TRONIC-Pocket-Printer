using System.IO;
using System.Text;
using TronicPocketPrinter.Core.Configuration;

namespace TronicPocketPrinter.Core.Logging;

/// <summary>
/// Minimal thread-safe file logger writing to
/// %LOCALAPPDATA%\TronicPocketPrinter\Logs.
/// Raster payloads are never written automatically.
/// </summary>
public sealed class FileLogger
{
    private static readonly object Sync = new();
    private readonly string _logFilePath;

    public static FileLogger Instance { get; } = new FileLogger();

    public string LogFolder { get; }

    public string? LastError { get; private set; }

    private FileLogger()
    {
        LogFolder = Path.Combine(PrinterSettings.AppDataFolder, "Logs");
        Directory.CreateDirectory(LogFolder);
        _logFilePath = Path.Combine(LogFolder, $"tronic-{DateTime.Now:yyyy-MM-dd}.log");
    }

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? ex = null)
    {
        LastError = ex is null ? message : $"{message}: {ex.Message}";
        var sb = new StringBuilder(message);
        if (ex is not null)
        {
            sb.AppendLine();
            sb.Append(ex);
        }

        Write("ERROR", sb.ToString());
    }

    private void Write(string level, string message)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            lock (Sync)
            {
                File.AppendAllText(_logFilePath, line);
            }
        }
        catch
        {
            // Logging must never crash the application.
        }
    }
}
