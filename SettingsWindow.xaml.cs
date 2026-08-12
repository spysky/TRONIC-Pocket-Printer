using System.Globalization;
using System.Windows;
using TronicPocketPrinter.Core.Configuration;
using TronicPocketPrinter.Core.Printing;

namespace TRONIC_Pocket_Printer;

public partial class SettingsWindow : Window
{
    private readonly PrinterSettings _settings;

    public SettingsWindow(PrinterSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        LoadValues();
        RefreshPorts();
    }

    private void LoadValues()
    {
        TxtPrinterName.Text = _settings.PrinterName;
        CmbPort.Text = _settings.PortName;
        TxtBaud.Text = _settings.BaudRate.ToString(CultureInfo.InvariantCulture);
        TxtWidth.Text = _settings.PrintWidthPixels.ToString(CultureInfo.InvariantCulture);
        TxtThreshold.Text = _settings.Threshold.ToString(CultureInfo.InvariantCulture);
        TxtFeedDots.Text = _settings.FeedDots.ToString(CultureInfo.InvariantCulture);
        TxtChunkSize.Text = _settings.ChunkSize.ToString(CultureInfo.InvariantCulture);
        TxtChunkDelay.Text = _settings.ChunkDelayMilliseconds.ToString(CultureInfo.InvariantCulture);
        CmbDither.SelectedIndex = _settings.DitherMode == DitherMode.FloydSteinberg ? 1 : 0;
        ChkAutoDetect.IsChecked = _settings.AutoDetectPort;
        ChkSavePreview.IsChecked = _settings.SaveProcessedPreview;
    }

    private void RefreshPorts()
    {
        var current = CmbPort.Text;
        CmbPort.Items.Clear();
        foreach (var p in PortDetector.ListPorts(_settings.ExpectedMac))
        {
            CmbPort.Items.Add(p.Display);
            if (p.IsLikelyPrinter)
            {
                current = p.PortName;
            }
        }
        CmbPort.Text = current;
    }

    private void BtnRefreshPorts_Click(object sender, RoutedEventArgs e) => RefreshPorts();

    private static string ExtractPort(string text)
    {
        int idx = text.IndexOf(' ');
        return idx > 0 ? text[..idx].Trim() : text.Trim();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.PrinterName = TxtPrinterName.Text.Trim();
            _settings.PortName = ExtractPort(CmbPort.Text);
            _settings.BaudRate = int.Parse(TxtBaud.Text, CultureInfo.InvariantCulture);
            _settings.PrintWidthPixels = int.Parse(TxtWidth.Text, CultureInfo.InvariantCulture);
            _settings.Threshold = int.Parse(TxtThreshold.Text, CultureInfo.InvariantCulture);
            _settings.FeedDots = int.Parse(TxtFeedDots.Text, CultureInfo.InvariantCulture);
            _settings.ChunkSize = int.Parse(TxtChunkSize.Text, CultureInfo.InvariantCulture);
            _settings.ChunkDelayMilliseconds = int.Parse(TxtChunkDelay.Text, CultureInfo.InvariantCulture);
            _settings.DitherMode = CmbDither.SelectedIndex == 1 ? DitherMode.FloydSteinberg : DitherMode.Threshold;
            _settings.AutoDetectPort = ChkAutoDetect.IsChecked == true;
            _settings.SaveProcessedPreview = ChkSavePreview.IsChecked == true;

            DialogResult = true;
        }
        catch (FormatException)
        {
            MessageBox.Show(this, "Please enter valid numeric values.", "Settings",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
