using TronicPocketPrinter.Core.Models;

namespace TronicPocketPrinter.Core.Imaging;

/// <summary>
/// Converts a <see cref="MonoBitmap"/> into the ESC/POS raster format
/// (GS v 0) understood by the TRONIC Mini Pocket Printer.
///
/// Packing:
///   - 1 bit per pixel, 8 horizontal pixels per byte
///   - MSB is the left-most pixel
///   - black = bit 1, white = bit 0
///
/// Do not change this without a documented reason: this format has been
/// validated against real hardware (384x16 all-black test print).
/// </summary>
public static class RasterConverter
{
    /// <summary>GS v 0 raster header prefix bytes: 1D 76 30 00.</summary>
    public static readonly byte[] RasterHeaderPrefix = { 0x1D, 0x76, 0x30, 0x00 };

    /// <summary>
    /// Builds the full GS v 0 header for the given width/height in pixels.
    /// Layout: 1D 76 30 00 xL xH yL yH where x is byte-width, y is pixel-height.
    /// </summary>
    public static byte[] BuildHeader(int widthPixels, int height)
    {
        int widthBytes = (widthPixels + 7) / 8;
        return new byte[]
        {
            0x1D, 0x76, 0x30, 0x00,
            (byte)(widthBytes & 0xFF),
            (byte)((widthBytes >> 8) & 0xFF),
            (byte)(height & 0xFF),
            (byte)((height >> 8) & 0xFF)
        };
    }

    /// <summary>
    /// Packs the mono bitmap pixel rows (no header) into raster bytes.
    /// Rows are padded to a byte boundary; padding bits are white (0).
    /// </summary>
    public static byte[] PackPixels(MonoBitmap bitmap)
    {
        int widthBytes = (bitmap.Width + 7) / 8;
        var data = new byte[widthBytes * bitmap.Height];

        for (int y = 0; y < bitmap.Height; y++)
        {
            int rowOffset = y * widthBytes;
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap[x, y])
                {
                    int byteIndex = rowOffset + (x >> 3);
                    int bit = 7 - (x & 7); // MSB = left-most pixel
                    data[byteIndex] |= (byte)(1 << bit);
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Builds a complete GS v 0 raster block: header followed by packed pixels.
    /// </summary>
    public static byte[] ToRaster(MonoBitmap bitmap)
    {
        var header = BuildHeader(bitmap.Width, bitmap.Height);
        var pixels = PackPixels(bitmap);
        var result = new byte[header.Length + pixels.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(pixels, 0, result, header.Length, pixels.Length);
        return result;
    }
}
