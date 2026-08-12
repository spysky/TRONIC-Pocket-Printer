using System.Text;
using System.Windows;
using TronicPocketPrinter.Core.Configuration;
using TronicPocketPrinter.Core.Imaging;
using TronicPocketPrinter.Core.Logging;
using TronicPocketPrinter.Core.Printing;

namespace TRONIC_Pocket_Printer;

public partial class DiagnosticsWindow : Window
{
    private readonly PrinterSettings _settings;
    private readonly PrinterService _printerService;
    private readonly FileLogger _log = FileLogger.Instance;

    public DiagnosticsWindow(PrinterSettings settings, PrinterService printerService)
    {
        InitializeComponent();
        _settings = settings;
        _printerService = printerService;
        BuildReport();
    }

    private void BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== TRONIC Pocket Printer Diagnostics ===");
        sb.AppendLine();

        sb.AppendLine("Available COM ports:");
        try
        {
            foreach (var p in PortDetector.ListPorts(_settings.ExpectedMac))
            {
                sb.AppendLine($"  {p.Display}{(p.IsLikelyPrinter ? "  <-- printer" : "")}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("  (error enumerating ports: " + ex.Message + ")");
        }

        sb.AppendLine();
        sb.AppendLine($"Selected/resolved port : {_printerService.ResolvePort()}");
        sb.AppendLine($"Configured port        : {_settings.PortName}");
        sb.AppendLine($"Auto detect            : {_settings.AutoDetectPort}");
        sb.AppendLine($"Expected MAC           : {_settings.ExpectedMac}");
        sb.AppendLine($"Print width            : {_settings.PrintWidthPixels} px");
        sb.AppendLine($"Baud rate              : {_settings.BaudRate}");
        sb.AppendLine();

        var job = _printerService.LastJob;
        sb.AppendLine("Last print job:");
        if (job is null)
        {
            sb.AppendLine("  (none this session)");
        }
        else
        {
            sb.AppendLine($"  Success   : {job.Success}");
            sb.AppendLine($"  Port      : {job.PortUsed}");
            sb.AppendLine($"  Bytes     : {job.BytesSent}");
            sb.AppendLine($"  Duration  : {job.Duration.TotalMilliseconds:F0} ms");
            if (job.ErrorMessage is not null)
            {
                sb.AppendLine($"  Error     : {job.ErrorMessage}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Last error : {_printerService.LastError ?? _log.LastError ?? "(none)"}");
        sb.AppendLine($"Log folder : {_log.LogFolder}");

        TxtReport.Text = sb.ToString();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => BuildReport();

    private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
    {
        await RunSafe(async () =>
        {
            bool ok = await _printerService.TestConnectionAsync();
            MessageBox.Show(this, ok ? "Connection OK." : "Could not open the printer port.",
                "Test Connection", MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async void BtnTestPrint_Click(object sender, RoutedEventArgs e)
    {
        await RunSafe(async () =>
        {
            string port = _printerService.ResolvePort();
            var mono = TestPatternGenerator.GenerateMono(_settings.PrintWidthPixels, port);
            await _printerService.PrintJobAsync(new[] { mono });
        });
    }

    private async void BtnFeed_Click(object sender, RoutedEventArgs e)
        => await RunSafe(() => _printerService.FeedAsync());

    private async Task RunSafe(Func<Task> action)
    {
        TxtBusy.Text = "Working...";
        IsEnabled = false;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _log.Error("Diagnostics action failed", ex);
            MessageBox.Show(this, ex.Message, "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
            TxtBusy.Text = "";
            BuildReport();
        }
    }
}
