using System.IO;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace TRONIC_Pocket_Printer;

/// <summary>
/// Helpers to bridge SkiaSharp bitmaps to WPF image sources.
/// </summary>
internal static class SkiaWpf
{
    public static BitmapImage ToBitmapImage(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var ms = new MemoryStream();
        data.SaveTo(ms);
        ms.Position = 0;

        var result = new BitmapImage();
        result.BeginInit();
        result.CacheOption = BitmapCacheOption.OnLoad;
        result.StreamSource = ms;
        result.EndInit();
        result.Freeze();
        return result;
    }
}
