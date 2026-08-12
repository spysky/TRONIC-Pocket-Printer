using PDFtoImage;
using SkiaSharp;

namespace TronicPocketPrinter.Core.Imaging;

/// <summary>
/// Renders PDF pages to SkiaSharp bitmaps using the PDFium-based PDFtoImage library.
/// </summary>
public static class PdfRenderer
{
    /// <summary>Returns the number of pages in a PDF file.</summary>
    public static int GetPageCount(string path)
    {
        using var stream = File.OpenRead(path);
        return Conversion.GetPageCount(stream);
    }

    /// <summary>
    /// Renders a single page (0-based) to an SKBitmap at the given DPI.
    /// </summary>
    public static SKBitmap RenderPage(string path, int pageIndex, int dpi = 203)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var options = new RenderOptions(Dpi: dpi, WithAspectRatio: true);
            return Conversion.ToImage(stream, page: (Index)pageIndex, options: options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("PDF could not be rendered.", ex);
        }
    }
}
