using System.IO.Ports;

namespace TronicPocketPrinter.Core.Printing;

/// <summary>
/// Handles the physical serial (Bluetooth SPP) communication with the printer.
/// Responsible for opening, chunked writing with delays, retry, timeouts and
/// cancellation. Kept separate from the printer protocol.
/// </summary>
public sealed class SerialPrinterTransport : IDisposable
{
    private SerialPort? _port;

    public int ChunkSize { get; set; } = 1024;
    public int ChunkDelayMilliseconds { get; set; } = 15;
    public int WriteTimeoutMilliseconds { get; set; } = 5000;

    /// <summary>Total bytes written since the last open.</summary>
    public long BytesWritten { get; private set; }

    public bool IsOpen => _port?.IsOpen ?? false;

    public string? PortName { get; private set; }

    /// <summary>
    /// Opens the given COM port, retrying a couple of times on transient failures.
    /// </summary>
    public async Task OpenAsync(string portName, int baudRate, CancellationToken ct = default)
    {
        Close();

        Exception? last = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var port = new SerialPort(portName, baudRate)
                {
                    WriteTimeout = WriteTimeoutMilliseconds,
                    ReadTimeout = WriteTimeoutMilliseconds,
                    Handshake = Handshake.None,
                    DtrEnable = true,
                    RtsEnable = true
                };
                port.Open();
                _port = port;
                PortName = portName;
                BytesWritten = 0;
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                last = ex; // port busy
                await Task.Delay(300, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(300, ct).ConfigureAwait(false);
            }
        }

        throw new IOException($"Could not open {portName}. The Bluetooth printer may be disconnected or the port is busy.", last);
    }

    /// <summary>
    /// Writes a payload in chunks, delaying between chunks so the small printer
    /// buffer is not overrun.
    /// </summary>
    public async Task WriteAsync(byte[] data, CancellationToken ct = default)
    {
        if (_port is null || !_port.IsOpen)
        {
            throw new InvalidOperationException("Serial port is not open.");
        }

        int offset = 0;
        while (offset < data.Length)
        {
            ct.ThrowIfCancellationRequested();
            int count = Math.Min(ChunkSize, data.Length - offset);
            _port.Write(data, offset, count);
            offset += count;
            BytesWritten += count;

            if (ChunkDelayMilliseconds > 0 && offset < data.Length)
            {
                await Task.Delay(ChunkDelayMilliseconds, ct).ConfigureAwait(false);
            }
        }
    }

    public void Close()
    {
        try
        {
            if (_port is not null)
            {
                if (_port.IsOpen)
                {
                    _port.Close();
                }
                _port.Dispose();
            }
        }
        catch
        {
            // Ignore errors on close.
        }
        finally
        {
            _port = null;
        }
    }

    public void Dispose() => Close();
}
