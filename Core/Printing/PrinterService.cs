using System.Diagnostics;
using TronicPocketPrinter.Core.Configuration;
using TronicPocketPrinter.Core.Logging;
using TronicPocketPrinter.Core.Models;

namespace TronicPocketPrinter.Core.Printing;

/// <summary>
/// Result / statistics of a completed print job.
/// </summary>
public sealed class PrintJobResult
{
    public bool Success { get; init; }
    public long BytesSent { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public string PortUsed { get; init; } = "";
}

/// <summary>
/// High-level orchestration of print jobs. Ensures only one physical print job
/// runs at a time and that the COM port is always closed, even on error.
/// </summary>
public sealed class PrinterService
{
    private readonly SemaphoreSlim _printLock = new(1, 1);
    private readonly PrinterSettings _settings;
    private readonly FileLogger _log = FileLogger.Instance;

    public string? LastError { get; private set; }
    public PrintJobResult? LastJob { get; private set; }

    public PrinterService(PrinterSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Resolves the port to use: auto-detected printer port if enabled and found,
    /// otherwise the configured PortName.
    /// </summary>
    public string ResolvePort()
    {
        if (_settings.AutoDetectPort)
        {
            var detected = PortDetector.DetectPrinterPort(_settings.ExpectedMac);
            if (detected is not null)
            {
                return detected.PortName;
            }
        }

        return _settings.PortName;
    }

    /// <summary>
    /// Prints one or more processed monochrome pages as a single job:
    /// open ? enable ? wake ? (raster + feed) per page ? stop ? close.
    /// </summary>
    public async Task<PrintJobResult> PrintJobAsync(
        IReadOnlyList<MonoBitmap> pages, CancellationToken ct = default)
    {
        if (pages.Count == 0)
        {
            throw new ArgumentException("No pages to print.", nameof(pages));
        }

        if (!await _printLock.WaitAsync(0, ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Printer port is busy.");
        }

        var sw = Stopwatch.StartNew();
        string port = ResolvePort();
        var transport = new SerialPrinterTransport
        {
            ChunkSize = _settings.ChunkSize,
            ChunkDelayMilliseconds = _settings.ChunkDelayMilliseconds
        };

        try
        {
            _log.Info($"Print job started. Port={port}, pages={pages.Count}");
            await transport.OpenAsync(port, _settings.BaudRate, ct).ConfigureAwait(false);

            var protocol = new TronicPrinterProtocol(transport) { FeedDots = _settings.FeedDots };

            await protocol.EnableAsync(ct).ConfigureAwait(false);
            await Task.Delay(100, ct).ConfigureAwait(false);
            await protocol.WakeAsync(ct).ConfigureAwait(false);
            await Task.Delay(100, ct).ConfigureAwait(false);

            foreach (var page in pages)
            {
                ct.ThrowIfCancellationRequested();
                _log.Info($"Sending raster {page.Width}x{page.Height}");
                await protocol.PrintImageAsync(page, ct).ConfigureAwait(false);
            }

            await protocol.StopJobAsync(ct).ConfigureAwait(false);

            sw.Stop();
            var result = new PrintJobResult
            {
                Success = true,
                BytesSent = transport.BytesWritten,
                Duration = sw.Elapsed,
                PortUsed = port
            };
            LastJob = result;
            LastError = null;
            _log.Info($"Print job finished. Bytes={result.BytesSent}, Duration={result.Duration.TotalMilliseconds:F0}ms");
            return result;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            LastError = "Print job cancelled.";
            _log.Warn("Print job cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            LastError = ex.Message;
            _log.Error("Print job failed", ex);
            var result = new PrintJobResult
            {
                Success = false,
                BytesSent = transport.BytesWritten,
                Duration = sw.Elapsed,
                ErrorMessage = ex.Message,
                PortUsed = port
            };
            LastJob = result;
            throw;
        }
        finally
        {
            transport.Close();
            _printLock.Release();
        }
    }

    /// <summary>
    /// Safe manual feed: open ? enable ? wake ? feed ? stop ? close.
    /// </summary>
    public async Task FeedAsync(CancellationToken ct = default)
    {
        if (!await _printLock.WaitAsync(0, ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Printer port is busy.");
        }

        string port = ResolvePort();
        var transport = new SerialPrinterTransport
        {
            ChunkSize = _settings.ChunkSize,
            ChunkDelayMilliseconds = _settings.ChunkDelayMilliseconds
        };

        try
        {
            _log.Info($"Manual feed. Port={port}");
            await transport.OpenAsync(port, _settings.BaudRate, ct).ConfigureAwait(false);
            var protocol = new TronicPrinterProtocol(transport) { FeedDots = _settings.FeedDots };

            await protocol.EnableAsync(ct).ConfigureAwait(false);
            await Task.Delay(100, ct).ConfigureAwait(false);
            await protocol.WakeAsync(ct).ConfigureAwait(false);
            await Task.Delay(100, ct).ConfigureAwait(false);
            await protocol.FeedAsync(_settings.FeedDots, ct).ConfigureAwait(false);
            await protocol.StopJobAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _log.Error("Feed failed", ex);
            throw;
        }
        finally
        {
            transport.Close();
            _printLock.Release();
        }
    }

    /// <summary>
    /// Attempts to open and immediately close the port to test connectivity.
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        if (!await _printLock.WaitAsync(0, ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Printer port is busy.");
        }

        string port = ResolvePort();
        var transport = new SerialPrinterTransport();
        try
        {
            await transport.OpenAsync(port, _settings.BaudRate, ct).ConfigureAwait(false);
            _log.Info($"Test connection OK on {port}");
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _log.Error("Test connection failed", ex);
            return false;
        }
        finally
        {
            transport.Close();
            _printLock.Release();
        }
    }
}
