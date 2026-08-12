using TronicPocketPrinter.Core.Imaging;
using TronicPocketPrinter.Core.Models;
using TronicPocketPrinter.Core.Printing;
using Xunit;

namespace TronicPocketPrinter.Tests;

public class RasterConverterTests
{
    private static MonoBitmap MakeRow(int width, Func<int, bool> black, int height = 1)
    {
        var mono = new MonoBitmap(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                mono[x, y] = black(x);
        return mono;
    }

    [Fact]
    public void WhiteLine_Produces48ZeroBytes()
    {
        var mono = MakeRow(384, _ => false);
        var packed = RasterConverter.PackPixels(mono);
        Assert.Equal(48, packed.Length);
        Assert.All(packed, b => Assert.Equal(0x00, b));
    }

    [Fact]
    public void BlackLine_Produces48FFBytes()
    {
        var mono = MakeRow(384, _ => true);
        var packed = RasterConverter.PackPixels(mono);
        Assert.Equal(48, packed.Length);
        Assert.All(packed, b => Assert.Equal(0xFF, b));
    }

    [Fact]
    public void AlternatingPixels_ProduceAA()
    {
        // black, white, black, white ... => 10101010 = 0xAA
        var mono = MakeRow(384, x => x % 2 == 0);
        var packed = RasterConverter.PackPixels(mono);
        Assert.All(packed, b => Assert.Equal(0xAA, b));
    }

    [Fact]
    public void Header_For384x16_IsCorrect()
    {
        var header = RasterConverter.BuildHeader(384, 16);
        Assert.Equal(new byte[] { 0x1D, 0x76, 0x30, 0x00, 0x30, 0x00, 0x10, 0x00 }, header);
    }

    [Fact]
    public void Header_HeightGreaterThan255_SplitsBytes()
    {
        var header = RasterConverter.BuildHeader(384, 300); // 300 = 0x012C
        Assert.Equal(0x2C, header[6]); // yL
        Assert.Equal(0x01, header[7]); // yH
    }

    [Fact]
    public void WidthNotMultipleOf8_PadsToByteBoundary()
    {
        // 10 pixels wide => 2 bytes per row, extra bits are white.
        var mono = MakeRow(10, _ => true);
        var packed = RasterConverter.PackPixels(mono);
        Assert.Equal(2, packed.Length);
        Assert.Equal(0xFF, packed[0]);
        Assert.Equal(0xC0, packed[1]); // top 2 bits set, rest padding = 0
    }
}

public class ProtocolTests
{
    [Fact]
    public void Enable_Command()
        => Assert.Equal(new byte[] { 0x10, 0xFF, 0xF1, 0x03 }, TronicPrinterProtocol.EnableCommand);

    [Fact]
    public void Wake_Command_Is12Zeros()
    {
        Assert.Equal(12, TronicPrinterProtocol.WakeCommand.Length);
        Assert.All(TronicPrinterProtocol.WakeCommand, b => Assert.Equal(0x00, b));
    }

    [Fact]
    public void Feed80_Command()
        => Assert.Equal(new byte[] { 0x1B, 0x4A, 0x50 }, TronicPrinterProtocol.BuildFeedCommand(80));

    [Fact]
    public void Stop_Command()
        => Assert.Equal(new byte[] { 0x10, 0xFF, 0xF1, 0x45 }, TronicPrinterProtocol.StopCommand);
}

public class ImageProcessorTests
{
    [Fact]
    public void Scaling_ToPrintWidth_Produces384()
    {
        using var src = new SkiaSharp.SKBitmap(800, 400);
        using (var canvas = new SkiaSharp.SKCanvas(src))
        {
            canvas.Clear(SkiaSharp.SKColors.Black);
        }

        var adj = new ImageAdjustments
        {
            PrintWidthPixels = 384,
            FitToWidth = true,
            TrimWhiteMargins = false
        };
        var mono = ImageProcessor.Process(src, adj);
        Assert.Equal(384, mono.Width);
        Assert.True(mono.Height > 0);
    }
}