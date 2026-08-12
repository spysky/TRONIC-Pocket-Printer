using SkiaSharp;
using TronicPocketPrinter.Core.Models;

namespace TronicPocketPrinter.Core.Imaging;

/// <summary>
/// Text alignment for printed notes.
/// </summary>
public enum TextAlign
{
    Left,
    Center,
    Right
}

/// <summary>
/// Renders plain text into a print-width bitmap so it can go through the same
/// raster pipeline as images. Acts as a simple "notepad" print source.
/// </summary>
public static class TextRenderer
{
    /// <summary>
    /// Renders the given text to an opaque SKBitmap of the requested width.
    /// Text is word-wrapped to the print width. The caller owns the result.
    /// </summary>
    public static SKBitmap Render(
        string text,
        int width,
        float fontSize = 26f,
        bool bold = false,
        TextAlign align = TextAlign.Left,
        int marginPx = 4,
        int topBottomPadding = 8)
    {
        text ??= string.Empty;

        var typeface = SKTypeface.FromFamilyName(
            "Segoe UI",
            bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            TextSize = fontSize,
            Typeface = typeface
        };

        int contentWidth = Math.Max(1, width - marginPx * 2);
        var lines = WrapText(text, paint, contentWidth);

        var metrics = paint.FontMetrics;
        float lineHeight = paint.TextSize * 1.35f;
        float ascent = -metrics.Ascent;

        int height = (int)Math.Ceiling(lines.Count * lineHeight) + topBottomPadding * 2;
        height = Math.Max(1, height);

        var bmp = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);

        float y = topBottomPadding + ascent;
        foreach (var line in lines)
        {
            float lineWidth = paint.MeasureText(line);
            float x = align switch
            {
                TextAlign.Center => marginPx + (contentWidth - lineWidth) / 2f,
                TextAlign.Right => width - marginPx - lineWidth,
                _ => marginPx
            };
            if (x < marginPx) x = marginPx;
            canvas.DrawText(line, x, y, paint);
            y += lineHeight;
        }

        return bmp;
    }

    /// <summary>
    /// Renders text directly to a monochrome bitmap ready for printing.
    /// </summary>
    public static MonoBitmap RenderMono(
        string text,
        int width,
        float fontSize = 26f,
        bool bold = false,
        TextAlign align = TextAlign.Left,
        int threshold = 128)
    {
        using var bmp = Render(text, width, fontSize, bold, align);
        var adj = new ImageAdjustments
        {
            PrintWidthPixels = width,
            FitToWidth = false,
            TrimWhiteMargins = false,
            Threshold = threshold
        };
        return ImageProcessor.Process(bmp, adj);
    }

    private static List<string> WrapText(string text, SKPaint paint, int maxWidth)
    {
        var result = new List<string>();
        // Preserve explicit line breaks.
        var rawLines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        foreach (var raw in rawLines)
        {
            if (raw.Length == 0)
            {
                result.Add(string.Empty);
                continue;
            }

            var words = raw.Split(' ');
            var current = string.Empty;

            foreach (var word in words)
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (paint.MeasureText(candidate) <= maxWidth || current.Length == 0)
                {
                    // If a single word is too long, hard-break it.
                    if (current.Length == 0 && paint.MeasureText(word) > maxWidth)
                    {
                        foreach (var piece in BreakLongWord(word, paint, maxWidth))
                        {
                            result.Add(piece);
                        }
                        current = string.Empty;
                    }
                    else
                    {
                        current = candidate;
                    }
                }
                else
                {
                    result.Add(current);
                    current = word;
                }
            }

            if (current.Length > 0)
            {
                result.Add(current);
            }
        }

        if (result.Count == 0)
        {
            result.Add(string.Empty);
        }

        return result;
    }

    private static IEnumerable<string> BreakLongWord(string word, SKPaint paint, int maxWidth)
    {
        var chunk = string.Empty;
        foreach (var ch in word)
        {
            string candidate = chunk + ch;
            if (paint.MeasureText(candidate) > maxWidth && chunk.Length > 0)
            {
                yield return chunk;
                chunk = ch.ToString();
            }
            else
            {
                chunk = candidate;
            }
        }

        if (chunk.Length > 0)
        {
            yield return chunk;
        }
    }
}
