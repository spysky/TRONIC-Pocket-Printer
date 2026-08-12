using SkiaSharp;
using TronicPocketPrinter.Core.Configuration;
using TronicPocketPrinter.Core.Models;

namespace TronicPocketPrinter.Core.Imaging;

/// <summary>
/// Adjustable image processing options applied before monochrome conversion.
/// </summary>
public sealed class ImageAdjustments
{
    /// <summary>-100..100</summary>
    public int Brightness { get; set; }
    /// <summary>-100..100</summary>
    public int Contrast { get; set; }
    /// <summary>0..255 threshold for black/white decision.</summary>
    public int Threshold { get; set; } = 128;
    public bool Invert { get; set; }
    /// <summary>Rotation in degrees, multiples of 90.</summary>
    public int RotationDegrees { get; set; }
    public bool FitToWidth { get; set; } = true;
    public bool TrimWhiteMargins { get; set; }
    public DitherMode DitherMode { get; set; } = DitherMode.Threshold;
    public int PrintWidthPixels { get; set; } = 384;
}

/// <summary>
/// Processes source images (composed onto white, resized to the print width,
/// adjusted, and converted to monochrome) using SkiaSharp.
/// </summary>
public static class ImageProcessor
{
    /// <summary>Loads an image file (jpg/png/bmp/webp) into an SKBitmap.</summary>
    public static SKBitmap LoadImage(string path)
    {
        using var stream = File.OpenRead(path);
        var bitmap = SKBitmap.Decode(stream)
            ?? throw new InvalidOperationException("Invalid or unsupported file.");
        return bitmap;
    }

    /// <summary>
    /// Composites onto white and returns an ARGB bitmap (opaque). Caller owns result.
    /// </summary>
    public static SKBitmap ComposeOnWhite(SKBitmap source)
    {
        var result = new SKBitmap(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    private static SKBitmap Rotate(SKBitmap source, int degrees)
    {
        degrees = ((degrees % 360) + 360) % 360;
        if (degrees == 0)
        {
            return source.Copy();
        }

        bool swap = degrees == 90 || degrees == 270;
        int w = swap ? source.Height : source.Width;
        int h = swap ? source.Width : source.Height;
        var rotated = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(rotated);
        canvas.Clear(SKColors.White);
        canvas.Translate(w / 2f, h / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        canvas.DrawBitmap(source, 0, 0);
        return rotated;
    }

    private static SKBitmap TrimWhite(SKBitmap source, int whiteThreshold = 250)
    {
        int minX = source.Width, minY = source.Height, maxX = -1, maxY = -1;
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                var c = source.GetPixel(x, y);
                int lum = (c.Red + c.Green + c.Blue) / 3;
                if (lum < whiteThreshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < 0)
        {
            return source.Copy(); // fully white
        }

        // small padding
        const int pad = 4;
        minX = Math.Max(0, minX - pad);
        minY = Math.Max(0, minY - pad);
        maxX = Math.Min(source.Width - 1, maxX + pad);
        maxY = Math.Min(source.Height - 1, maxY + pad);

        int w = maxX - minX + 1;
        int h = maxY - minY + 1;
        var cropped = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(cropped);
        canvas.DrawBitmap(source, new SKRect(minX, minY, maxX + 1, maxY + 1),
            new SKRect(0, 0, w, h));
        return cropped;
    }

    private static SKBitmap ResizeToWidth(SKBitmap source, int targetWidth, bool fitToWidth)
    {
        int newWidth;
        if (source.Width > targetWidth)
        {
            newWidth = targetWidth;
        }
        else
        {
            newWidth = fitToWidth ? targetWidth : source.Width;
        }

        if (newWidth == source.Width)
        {
            return source.Copy();
        }

        int newHeight = Math.Max(1, (int)Math.Round(source.Height * (newWidth / (double)source.Width)));
        var info = new SKImageInfo(newWidth, newHeight, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var resized = new SKBitmap(info);
        using (var canvas = new SKCanvas(resized))
        {
            canvas.Clear(SKColors.White);
        }
        source.ScalePixels(resized, SKFilterQuality.High);
        return resized;
    }

    private static double ApplyBrightnessContrast(double value, int brightness, int contrast)
    {
        // value 0..255
        value += brightness * 255.0 / 100.0;
        double c = contrast / 100.0; // -1..1
        double factor = (1.0 + c) / (1.0 - c * 0.999);
        value = factor * (value - 128.0) + 128.0;
        return value;
    }

    /// <summary>
    /// Full processing pipeline: compose on white ? rotate ? trim ? resize ?
    /// brightness/contrast ? monochrome (threshold or Floyd-Steinberg).
    /// The source bitmap is not disposed by this method.
    /// </summary>
    public static MonoBitmap Process(SKBitmap source, ImageAdjustments adj)
    {
        var composed = ComposeOnWhite(source);
        try
        {
            var rotated = Rotate(composed, adj.RotationDegrees);
            try
            {
                SKBitmap trimmed = adj.TrimWhiteMargins ? TrimWhite(rotated) : rotated.Copy();
                try
                {
                    var resized = ResizeToWidth(trimmed, adj.PrintWidthPixels, adj.FitToWidth);
                    try
                    {
                        return ToMono(resized, adj);
                    }
                    finally { resized.Dispose(); }
                }
                finally { trimmed.Dispose(); }
            }
            finally { rotated.Dispose(); }
        }
        finally { composed.Dispose(); }
    }

    private static MonoBitmap ToMono(SKBitmap bmp, ImageAdjustments adj)
    {
        int w = bmp.Width, h = bmp.Height;

        // Build grayscale buffer with brightness/contrast applied.
        var gray = new double[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var c = bmp.GetPixel(x, y);
                double lum = (c.Red + c.Green + c.Blue) / 3.0;
                lum = ApplyBrightnessContrast(lum, adj.Brightness, adj.Contrast);
                gray[y * w + x] = lum;
            }
        }

        var mono = new MonoBitmap(w, h);

        if (adj.DitherMode == DitherMode.FloydSteinberg)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    double oldPixel = gray[idx];
                    double newPixel = oldPixel < adj.Threshold ? 0 : 255;
                    double error = oldPixel - newPixel;

                    bool black = newPixel < 128;
                    if (adj.Invert) black = !black;
                    mono[x, y] = black;

                    Diffuse(gray, w, h, x + 1, y, error * 7 / 16);
                    Diffuse(gray, w, h, x - 1, y + 1, error * 3 / 16);
                    Diffuse(gray, w, h, x, y + 1, error * 5 / 16);
                    Diffuse(gray, w, h, x + 1, y + 1, error * 1 / 16);
                }
            }
        }
        else
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool black = gray[y * w + x] < adj.Threshold;
                    if (adj.Invert) black = !black;
                    mono[x, y] = black;
                }
            }
        }

        return mono;
    }

    private static void Diffuse(double[] gray, int w, int h, int x, int y, double delta)
    {
        if (x < 0 || x >= w || y < 0 || y >= h) return;
        gray[y * w + x] += delta;
    }

    /// <summary>
    /// Renders a MonoBitmap to an opaque SKBitmap for on-screen preview.
    /// </summary>
    public static SKBitmap MonoToPreview(MonoBitmap mono)
    {
        var bmp = new SKBitmap(mono.Width, mono.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        for (int y = 0; y < mono.Height; y++)
        {
            for (int x = 0; x < mono.Width; x++)
            {
                bmp.SetPixel(x, y, mono[x, y] ? SKColors.Black : SKColors.White);
            }
        }
        return bmp;
    }
}
