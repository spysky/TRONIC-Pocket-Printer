using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TronicPocketPrinter.Core.Configuration;

/// <summary>
/// Image conversion / dithering mode.
/// </summary>
public enum DitherMode
{
    Threshold,
    FloydSteinberg
}

/// <summary>
/// Persisted application settings stored in
/// %LOCALAPPDATA%\TronicPocketPrinter\settings.json
/// </summary>
public sealed class PrinterSettings
{
    public string PrinterName { get; set; } = "TRONIC Mini Pocket Printer";
    public string PortName { get; set; } = "COM6";
    public int BaudRate { get; set; } = 115200;
    public int PrintWidthPixels { get; set; } = 384;
    public int Threshold { get; set; } = 128;
    public int FeedDots { get; set; } = 80;
    public int ChunkSize { get; set; } = 1024;
    public int ChunkDelayMilliseconds { get; set; } = 15;
    public bool AutoDetectPort { get; set; } = true;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DitherMode DitherMode { get; set; } = DitherMode.Threshold;

    /// <summary>Bluetooth MAC of the TRONIC printer (used for auto-detect).</summary>
    public string ExpectedMac { get; set; } = "55:55:09:41:D5:14";

    /// <summary>Fit images/pages smaller than the print width to full width.</summary>
    public bool FitToWidth { get; set; } = true;

    /// <summary>Trim excessive white margins from PDF pages before scaling.</summary>
    public bool TrimWhiteMargins { get; set; } = true;

    /// <summary>Save the processed monochrome preview to the log folder for debugging.</summary>
    public bool SaveProcessedPreview { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string AppDataFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TronicPocketPrinter");

    public static string SettingsFilePath => Path.Combine(AppDataFolder, "settings.json");

    public static PrinterSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<PrinterSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // Corrupt or unreadable settings fall back to defaults.
        }

        return new PrinterSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDataFolder);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }

    public PrinterSettings Clone() => (PrinterSettings)MemberwiseClone();
}
