using TronicPocketPrinter.Core.Imaging;
using TronicPocketPrinter.Core.Models;

namespace TronicPocketPrinter.Core.Printing;

/// <summary>
/// Implements the TRONIC / Mini Pocket Printer command protocol extracted from
/// the official Android SDK (com.luckprinter.sdk_new ... MiniPocketPrinter ? DP_D1).
///
/// VALIDATED against real hardware. Do not change the command constants without
/// a documented reason.
/// </summary>
public sealed class TronicPrinterProtocol
{
    // enablePrinterLuck()
    public static readonly byte[] EnableCommand = { 0x10, 0xFF, 0xF1, 0x03 };

    // printerWakeupLuck() – 12 zero bytes
    public static readonly byte[] WakeCommand = new byte[12];

    // stopPrintJobLuck()
    public static readonly byte[] StopCommand = { 0x10, 0xFF, 0xF1, 0x45 };

    private readonly SerialPrinterTransport _transport;

    public int FeedDots { get; set; } = 80;

    public TronicPrinterProtocol(SerialPrinterTransport transport)
    {
        _transport = transport;
    }

    /// <summary>Builds the feed command: 1B 4A n.</summary>
    public static byte[] BuildFeedCommand(int dots)
        => new byte[] { 0x1B, 0x4A, (byte)(dots & 0xFF) };

    public Task EnableAsync(CancellationToken ct = default)
        => _transport.WriteAsync(EnableCommand, ct);

    public Task WakeAsync(CancellationToken ct = default)
        => _transport.WriteAsync(WakeCommand, ct);

    public Task PrintRasterAsync(MonoBitmap bitmap, CancellationToken ct = default)
        => _transport.WriteAsync(RasterConverter.ToRaster(bitmap), ct);

    public Task FeedAsync(int? dots = null, CancellationToken ct = default)
        => _transport.WriteAsync(BuildFeedCommand(dots ?? FeedDots), ct);

    public Task StopJobAsync(CancellationToken ct = default)
        => _transport.WriteAsync(StopCommand, ct);

    /// <summary>
    /// Prints a single already-processed monochrome image within an
    /// already-open connection: raster + feed. Does NOT send enable/wake/stop.
    /// </summary>
    public async Task PrintImageAsync(MonoBitmap bitmap, CancellationToken ct = default)
    {
        await PrintRasterAsync(bitmap, ct).ConfigureAwait(false);
        await FeedAsync(FeedDots, ct).ConfigureAwait(false);
    }
}
