using SkiaSharp;
using TronicPocketPrinter.Core.Models;

namespace TronicPocketPrinter.Core.Imaging;

/// <summary>
/// Generates an internal test pattern (borders, bars, checkerboard, line scale
/// and text) that is fed through the same raster pipeline.
/// </summary>
public static class TestPatternGenerator
{
    public static SKBitmap Generate(int width, string portName)
    {
        int height = 420;
        var bmp = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);

        using var black = new SKPaint { Color = SKColors.Black, IsAntialias = false };
        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            TextSize = 22,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        using var smallText = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            TextSize = 18,
            Typeface = SKTypeface.FromFamilyName("Segoe UI")
        };

        int y = 6;

        // Title text
        canvas.DrawText("TRONIC Mini Pocket Printer", 4, y + 20, textPaint);
        y += 30;
        canvas.DrawText($"{width} pixels", 4, y + 18, smallText);
        y += 24;
        canvas.DrawText($"COM: {portName}", 4, y + 18, smallText);
        y += 30;

        // Left / right / center vertical lines
        canvas.DrawRect(0, y, 4, 60, black);                       // left edge
        canvas.DrawRect(width - 4, y, 4, 60, black);               // right edge
        canvas.DrawRect(width / 2 - 2, y, 4, 60, black);           // center
        y += 70;

        // Horizontal bars of varying thickness
        for (int t = 1; t <= 6; t++)
        {
            canvas.DrawRect(0, y, width, t, black);
            y += t + 6;
        }
        y += 4;

        // Checkerboard
        int cell = 8;
        for (int cy = 0; cy < 64; cy += cell)
        {
            for (int cx = 0; cx < width; cx += cell)
            {
                if (((cx / cell) + (cy / cell)) % 2 == 0)
                {
                    canvas.DrawRect(cx, y + cy, cell, cell, black);
                }
            }
        }
        y += 64 + 8;

        // Line scale (increasing gaps)
        for (int i = 0; i < width; i += 2)
        {
            canvas.DrawRect(i, y, 1, 24, black);
        }
        y += 34;

        canvas.DrawText("Test print OK", 4, y + 18, smallText);

        return bmp;
    }

    public static MonoBitmap GenerateMono(int width, string portName)
    {
        using var bmp = Generate(width, portName);
        var adj = new ImageAdjustments
        {
            PrintWidthPixels = width,
            FitToWidth = false,
            TrimWhiteMargins = false,
            Threshold = 128
        };
        return ImageProcessor.Process(bmp, adj);
    }
}
