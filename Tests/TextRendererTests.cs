using TronicPocketPrinter.Core.Imaging;
using Xunit;

namespace TronicPocketPrinter.Tests;

public class TextRendererTests
{
    [Fact]
    public void RenderMono_ProducesRequestedWidth()
    {
        var mono = TextRenderer.RenderMono("Hello TRONIC", 384);
        Assert.Equal(384, mono.Width);
        Assert.True(mono.Height > 0);
    }

    [Fact]
    public void RenderMono_ContainsBlackPixels_ForNonEmptyText()
    {
        var mono = TextRenderer.RenderMono("Test", 384, fontSize: 30f, bold: true);
        Assert.Contains(mono.Pixels, p => p);
    }

    [Fact]
    public void RenderMono_EmptyText_IsAllWhite()
    {
        var mono = TextRenderer.RenderMono(string.Empty, 384);
        Assert.Equal(384, mono.Width);
        Assert.DoesNotContain(mono.Pixels, p => p);
    }

    [Fact]
    public void RenderMono_MultilineText_IncreasesHeight()
    {
        var single = TextRenderer.RenderMono("Line one", 384, fontSize: 26f);
        var multi = TextRenderer.RenderMono("Line one\nLine two\nLine three", 384, fontSize: 26f);
        Assert.True(multi.Height > single.Height);
    }
}
