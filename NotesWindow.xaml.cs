using System.Windows;
using TronicPocketPrinter.Core.Configuration;
using TronicPocketPrinter.Core.Imaging;
using TronicPocketPrinter.Core.Logging;
using TronicPocketPrinter.Core.Models;
using TronicPocketPrinter.Core.Printing;

namespace TRONIC_Pocket_Printer;

public partial class NotesWindow : Window
{
    private readonly PrinterSettings _settings;
    private readonly PrinterService _printerService;
    private readonly FileLogger _log = FileLogger.Instance;
    private bool _isPrinting;

    public NotesWindow(PrinterSettings settings, PrinterService printerService)
    {
        InitializeComponent();
        _settings = settings;
        _printerService = printerService;
        Loaded += (_, _) => UpdatePreview();
    }

    private TextAlign SelectedAlign => CmbAlign.SelectedIndex switch
    {
        1 => TextAlign.Center,
        2 => TextAlign.Right,
        _ => TextAlign.Left
    };

    private MonoBitmap BuildMono() => TextRenderer.RenderMono(
        TxtInput.Text,
        _settings.PrintWidthPixels,
        (float)SldFontSize.Value,
        ChkBold.IsChecked == true,
        SelectedAlign,
        _settings.Threshold);

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (TxtFontSize is not null)
        {
            TxtFontSize.Text = ((int)SldFontSize.Value).ToString();
        }

        try
        {
            var mono = BuildMono();
            using var preview = ImageProcessor.MonoToPreview(mono);
            ImgPreview.Source = SkiaWpf.ToBitmapImage(preview);
            TxtInfo.Text = $"{_settings.PrintWidthPixels} px wide  •  {mono.Height} px tall";
        }
        catch (Exception ex)
        {
            _log.Warn("Notes preview failed: " + ex.Message);
        }
    }

    private async void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        if (_isPrinting) { Warn("Printer port is busy."); return; }
        if (string.IsNullOrWhiteSpace(TxtInput.Text)) { Warn("Nothing to print."); return; }

        _isPrinting = true;
        BtnPrint.IsEnabled = false;
        try
        {
            var mono = BuildMono();
            await _printerService.PrintJobAsync(new[] { mono });
        }
        catch (Exception ex)
        {
            _log.Error("Notes print failed", ex);
            Warn(ex.Message);
        }
        finally
        {
            _isPrinting = false;
            BtnPrint.IsEnabled = true;
        }
    }

    private void Warn(string message)
        => MessageBox.Show(this, message, "Notes", MessageBoxButton.OK, MessageBoxImage.Warning);
}
