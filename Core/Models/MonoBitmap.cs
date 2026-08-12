namespace TronicPocketPrinter.Core.Models;

/// <summary>
/// A 1-bit-per-pixel bitmap where <c>true</c> means a black (printed) pixel.
/// This is the intermediate representation fed to the raster converter.
/// </summary>
public sealed class MonoBitmap
{
    public int Width { get; }
    public int Height { get; }

    /// <summary>Row-major pixel data; <c>true</c> = black.</summary>
    public bool[] Pixels { get; }

    public MonoBitmap(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new bool[width * height];
    }

    public bool this[int x, int y]
    {
        get => Pixels[y * Width + x];
        set => Pixels[y * Width + x] = value;
    }
}
