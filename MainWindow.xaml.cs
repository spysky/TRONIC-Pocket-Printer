using System.IO;
using System.Windows;
using System.Windows.Input;
using SkiaSharp;
using TronicPocketPrinter.Core.Configuration;
using TronicPocketPrinter.Core.Imaging;
using TronicPocketPrinter.Core.Logging;
using TronicPocketPrinter.Core.Models;
using TronicPocketPrinter.Core.Printing;

namespace TRONIC_Pocket_Printer;

public partial class MainWindow : Window
{
    private readonly PrinterSettings _settings;
    private readonly PrinterService _printerService;
    private readonly FileLogger _log = FileLogger.Instance;

    private static readonly string[] ImageExtensions =
        { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };

    private string? _currentFile;
    private bool _isPdf;
    private int _pageCount;
    private int _currentPage;         // 0-based
    private SKBitmap? _sourceBitmap;  // current page/image source (composed later)
    private MonoBitmap? _currentMono;
    private CancellationTokenSource? _printCts;
    private bool _isPrinting;

    public MainWindow()
    {
        InitializeComponent();
        _settings = PrinterSettings.Load();
        _printerService = new PrinterService(_settings);
        _log.Info("Application started.");
        Loaded += (_, _) => RefreshConnectionStatus();
    }

    private ImageAdjustments BuildAdjustments() => new()
    {
        Brightness = (int)SldBrightness.Value,
        Contrast = (int)SldContrast.Value,
        Threshold = (int)SldThreshold.Value,
        Invert = ChkInvert.IsChecked == true,
        RotationDegrees = (_rotation + OrientationRotation) % 360,
        FitToWidth = ChkFitWidth.IsChecked == true,
        TrimWhiteMargins = ChkTrim.IsChecked == true,
        DitherMode = CmbDither.SelectedIndex == 1 ? DitherMode.FloydSteinberg : DitherMode.Threshold,
        PrintWidthPixels = _settings.PrintWidthPixels
    };

    /// <summary>
    /// Extra rotation applied for the selected page orientation. Because the paper
    /// is a continuous roll, Landscape simply rotates the content 90° so its long
    /// side runs along the (near-infinite) paper feed.
    /// </summary>
    private int OrientationRotation => CmbOrientation?.SelectedIndex == 1 ? 90 : 0;

    private int _rotation;

    // ---------- File opening ----------

    private void BtnOpen_Click(object sender, RoutedEventArgs e) => OpenFileDialog();
    private void Open_Executed(object sender, ExecutedRoutedEventArgs e) => OpenFileDialog();

    private void OpenFileDialog()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Supported files|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.pdf|All files|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            LoadFile(dlg.FileName);
        }
    }

    private void LoadFile(string path)
    {
        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            _rotation = 0;

            if (ext == ".pdf")
            {
                _isPdf = true;
                _pageCount = PdfRenderer.GetPageCount(path);
                _currentPage = 0;
            }
            else if (Array.IndexOf(ImageExtensions, ext) >= 0)
            {
                _isPdf = false;
                _pageCount = 1;
                _currentPage = 0;
            }
            else
            {
                ShowError("Invalid or unsupported file.");
                return;
            }

            _currentFile = path;
            _log.Info($"Opened file: {path} (pdf={_isPdf}, pages={_pageCount})");
            LoadCurrentPage();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            _log.Error("Failed to open file", ex);
            ShowError("Invalid or unsupported file.");
        }
    }

    private void LoadCurrentPage()
    {
        _sourceBitmap?.Dispose();
        _sourceBitmap = null;

        if (_currentFile is null) return;

        try
        {
            _sourceBitmap = _isPdf
                ? PdfRenderer.RenderPage(_currentFile, _currentPage, 203)
                : ImageProcessor.LoadImage(_currentFile);
            UpdatePreview();
        }
        catch (Exception ex)
        {
            _log.Error("Failed to render page", ex);
            ShowError(_isPdf ? "PDF could not be rendered." : "Invalid or unsupported file.");
        }
    }

    // ---------- Preview ----------

    private void UpdatePreview()
    {
        if (_sourceBitmap is null) return;

        var adj = BuildAdjustments();
        _currentMono = ImageProcessor.Process(_sourceBitmap, adj);

        bool printPreview = CmbViewMode.SelectedIndex != 0;
        if (printPreview)
        {
            using var mono = ImageProcessor.MonoToPreview(_currentMono);
            ImgPreview.Source = SkiaWpf.ToBitmapImage(mono);
        }
        else
        {
            ImgPreview.Source = SkiaWpf.ToBitmapImage(_sourceBitmap);
        }

        ApplyZoom();

        if (_settings.SaveProcessedPreview && _currentMono is not null)
        {
            SaveProcessedPreview(_currentMono);
        }

        UpdatePdfButtons();
    }

    private void SaveProcessedPreview(MonoBitmap mono)
    {
        try
        {
            using var bmp = ImageProcessor.MonoToPreview(mono);
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            var file = Path.Combine(_log.LogFolder, $"preview-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            using var fs = File.Create(file);
            data.SaveTo(fs);
        }
        catch (Exception ex)
        {
            _log.Warn("Could not save processed preview: " + ex.Message);
        }
    }

    private void ApplyZoom()
    {
        if (ImgPreview.Source is null) return;
        double scale = SldZoom?.Value ?? 1;
        ImgPreview.Width = ImgPreview.Source.Width * scale;
        ImgPreview.Height = ImgPreview.Source.Height * scale;
    }

    private void Adjustment_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdatePreview();
    }

    private void Slider_DragCompleted(object sender, RoutedEventArgs e) => UpdatePreview();

    private void SldZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyZoom();

    private void BtnRotateLeft_Click(object sender, RoutedEventArgs e)
    {
        _rotation = (_rotation + 270) % 360;
        UpdatePreview();
    }

    private void BtnRotateRight_Click(object sender, RoutedEventArgs e)
    {
        _rotation = (_rotation + 90) % 360;
        UpdatePreview();
    }

    // ---------- PDF navigation ----------

    private void UpdatePdfButtons()
    {
        bool multi = _isPdf && _pageCount > 1;
        BtnPrevPage.IsEnabled = multi && _currentPage > 0;
        BtnNextPage.IsEnabled = multi && _currentPage < _pageCount - 1;
        BtnPrintCurrent.IsEnabled = _isPdf && _currentFile is not null && !_isPrinting;
        BtnPrintAll.IsEnabled = _isPdf && _currentFile is not null && !_isPrinting;
    }

    private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 0)
        {
            _currentPage--;
            LoadCurrentPage();
            UpdateStatus();
        }
    }

    private void BtnNextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage < _pageCount - 1)
        {
            _currentPage++;
            LoadCurrentPage();
            UpdateStatus();
        }
    }

    private async void BtnPrintCurrent_Click(object sender, RoutedEventArgs e) => await PrintPagesAsync(currentOnly: true);
    private async void BtnPrintAll_Click(object sender, RoutedEventArgs e) => await PrintPagesAsync(currentOnly: false);

    // ---------- Printing ----------

    private void BtnPrint_Click(object sender, RoutedEventArgs e) => _ = PrintPagesAsync(currentOnly: true);
    private void Print_Executed(object sender, ExecutedRoutedEventArgs e) => _ = PrintPagesAsync(currentOnly: true);

    private async Task PrintPagesAsync(bool currentOnly)
    {
        if (_isPrinting)
        {
            ShowError("Printer port is busy.");
            return;
        }
        if (_currentFile is null || _sourceBitmap is null)
        {
            ShowError("Open a file first.");
            return;
        }

        var pages = new List<MonoBitmap>();
        var adj = BuildAdjustments();

        try
        {
            if (_isPdf && !currentOnly)
            {
                for (int i = 0; i < _pageCount; i++)
                {
                    using var page = PdfRenderer.RenderPage(_currentFile, i, 203);
                    pages.Add(ImageProcessor.Process(page, adj));
                }
            }
            else
            {
                pages.Add(ImageProcessor.Process(_sourceBitmap, adj));
            }
        }
        catch (Exception ex)
        {
            _log.Error("Failed to prepare pages", ex);
            ShowError(_isPdf ? "PDF could not be rendered." : "Invalid or unsupported file.");
            return;
        }

        await RunPrintAsync(() => _printerService.PrintJobAsync(pages, _printCts!.Token));
    }

    private async void BtnFeed_Click(object sender, RoutedEventArgs e)
    {
        if (_isPrinting) { ShowError("Printer port is busy."); return; }
        await RunPrintAsync(() => _printerService.FeedAsync(_printCts!.Token));
    }

    private async void BtnTestPrint_Click(object sender, RoutedEventArgs e)
    {
        if (_isPrinting) { ShowError("Printer port is busy."); return; }
        string port = _printerService.ResolvePort();
        var mono = TestPatternGenerator.GenerateMono(_settings.PrintWidthPixels, port);
        await RunPrintAsync(() => _printerService.PrintJobAsync(new[] { mono }, _printCts!.Token));
    }

    private async Task RunPrintAsync(Func<Task> action)
    {
        _printCts = new CancellationTokenSource();
        SetPrintingState(true);
        try
        {
            await action();
            TxtStatus.Text = "Ready";
            RefreshConnectionStatus();
        }
        catch (OperationCanceledException)
        {
            TxtStatus.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            _log.Error("Print operation failed", ex);
            ShowError(FriendlyError(ex));
            TxtStatus.Text = "Error";
        }
        finally
        {
            SetPrintingState(false);
            _printCts?.Dispose();
            _printCts = null;
        }
    }

    private static string FriendlyError(Exception ex) => ex switch
    {
        InvalidOperationException => ex.Message,
        IOException => "Could not open the printer port. The Bluetooth printer may be disconnected.",
        UnauthorizedAccessException => "Printer port is busy.",
        _ => "Printer not found or an unexpected error occurred."
    };

    private void SetPrintingState(bool printing)
    {
        _isPrinting = printing;
        BtnPrint.IsEnabled = !printing;
        BtnFeed.IsEnabled = !printing;
        BtnTestPrint.IsEnabled = !printing;
        BtnOpen.IsEnabled = !printing;
        BtnCancel.IsEnabled = printing;
        TxtStatus.Text = printing ? "Printing..." : "Ready";
        UpdatePdfButtons();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => _printCts?.Cancel();

    // ---------- Settings / Diagnostics ----------

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_settings) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _settings.Save();
            RefreshConnectionStatus();
            UpdatePreview();
        }
    }

    private void BtnDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var win = new DiagnosticsWindow(_settings, _printerService) { Owner = this };
        win.ShowDialog();
        RefreshConnectionStatus();
    }

    private void BtnNotes_Click(object sender, RoutedEventArgs e)
    {
        var win = new NotesWindow(_settings, _printerService) { Owner = this };
        win.ShowDialog();
        RefreshConnectionStatus();
    }

    // ---------- Status ----------

    private void RefreshConnectionStatus()
    {
        try
        {
            var detected = PortDetector.DetectPrinterPort(_settings.ExpectedMac);
            if (detected is not null)
            {
                TxtConnection.Text = "Printer connected";
                TxtPort.Text = $"Port: {detected.Display}";
            }
            else
            {
                TxtConnection.Text = "Disconnected";
                TxtPort.Text = $"Port: {_settings.PortName} (configured)";
            }
        }
        catch
        {
            TxtConnection.Text = "Disconnected";
        }
    }

    private void UpdateStatus()
    {
        TxtFile.Text = _currentFile is null ? "No file" : Path.GetFileName(_currentFile);
        TxtPage.Text = _isPdf ? $"Page: {_currentPage + 1} / {_pageCount}" : "Page: 1 / 1";
    }

    // ---------- Drag & drop ----------

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            LoadFile(files[0]);
        }
    }

    private void ShowError(string message)
        => MessageBox.Show(this, message, "TRONIC Pocket Printer", MessageBoxButton.OK, MessageBoxImage.Warning);
}
