using ScreenshotApp.Capture;

namespace ScreenshotApp.Tests;

public class ScrollStitcherTests
{
    internal static ScrollFrame Frame(int offset, int height = 96, int header = 0, int footer = 0, int width = 7)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var row = y < header ? -1000 - y : y >= height - footer ? -2000 - (height - y) : offset + y - header;
            for (var x = 0; x < width; x++)
            {
                var seed = unchecked((uint)(row * 747796405 + x * 2891336453L));
                seed = ((seed >> ((int)(seed >> 28) + 4)) ^ seed) * 277803737U;
                seed = (seed >> 22) ^ seed;
                for (var c = 0; c < 3; c++) pixels[(y * width + x) * 4 + c] = (byte)(seed >> (c * 8));
            }
        }
        return new ScrollFrame(width, height, pixels);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(19, 0, 0)]
    [InlineData(60, 0, 0)]
    [InlineData(1, 7, 9)]
    [InlineData(21, 7, 9)]
    [InlineData(21, 7, 0)]
    [InlineData(21, 0, 9)]
    public void AT16_DownwardFrames_AreExactlyContinuous(int shift, int header, int footer)
    {
        var stitcher = new ScrollStitcher(Frame(0, header: header, footer: footer));
        for (var i = 1; i <= 4; i++)
            Assert.Equal(StitchStatus.Appended, stitcher.Append(Frame(i * shift, header: header, footer: footer)));
        Assert.Equal(96 + 4 * shift, stitcher.Height);
        Assert.Equal(Frame(0, stitcher.Height, header, footer).Pixels, stitcher.GetPixels());
    }

    [Fact]
    public void AT16_BackwardAndUnrelated_DoNotChangeAcceptedPixels()
    {
        var stitcher = new ScrollStitcher(Frame(0));
        Assert.Equal(StitchStatus.Appended, stitcher.Append(Frame(20)));
        var accepted = stitcher.GetPixels();
        Assert.Equal(StitchStatus.Backward, stitcher.Append(Frame(8)));
        Assert.Equal(StitchStatus.CannotAlign, stitcher.Append(Frame(2000)));
        Assert.Equal(accepted, stitcher.GetPixels());
        Assert.Equal(StitchStatus.Appended, stitcher.Append(Frame(40)));
        Assert.Equal(Frame(0, 136).Pixels, stitcher.GetPixels());
    }

    [Fact]
    public void AT16_UnchangedAndUndefinedAlpha_DoNotAppend()
    {
        var first = Frame(0);
        var pixels = (byte[])first.Pixels.Clone();
        for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        var stitcher = new ScrollStitcher(first);
        Assert.Equal(StitchStatus.Unchanged, stitcher.Append(new ScrollFrame(first.Width, first.Height, pixels)));
        Assert.Equal(first.Pixels, stitcher.GetPixels());
    }

    [Fact]
    public void AT16_RepeatedBlocks_AreAmbiguousAndNeverGuessed()
    {
        ScrollFrame Repeated(int offset)
        {
            var pattern = Frame(0);
            var pixels = new byte[pattern.Pixels.Length];
            for (var y = 0; y < 96; y++)
                pattern.Pixels.AsSpan(((y + offset) % 13) * pattern.Stride, pattern.Stride).CopyTo(pixels.AsSpan(y * pattern.Stride));
            return new ScrollFrame(pattern.Width, 96, pixels);
        }
        var first = Repeated(0);
        var stitcher = new ScrollStitcher(first);
        Assert.Equal(StitchStatus.CannotAlign, stitcher.Append(Repeated(5)));
        Assert.Equal(first.Pixels, stitcher.GetPixels());
    }

    [Fact]
    public void AT16_DynamicPixelOrChangingHeader_RejectsWithoutDamage()
    {
        var stitcher = new ScrollStitcher(Frame(0, header: 7, footer: 9));
        Assert.Equal(StitchStatus.Appended, stitcher.Append(Frame(20, header: 7, footer: 9)));
        var next = Frame(40, header: 7, footer: 9);
        next.Pixels[0] ^= 1;
        Assert.Equal(StitchStatus.CannotAlign, stitcher.Append(new ScrollFrame(next.Width, next.Height, next.Pixels)));
        Assert.Equal(116, stitcher.Height);
        Assert.Equal(StitchStatus.CannotAlign, stitcher.Append(Frame(40, width: 8)));
    }

    [Fact]
    public void AT16_HeightLimit_AppendsOnlyCompleteRowsAndKeepsFooter()
    {
        var stitcher = new ScrollStitcher(Frame(0, 32760, 2, 3, 2));
        Assert.Equal(StitchStatus.LimitReached, stitcher.Append(Frame(20, 32760, 2, 3, 2)));
        Assert.Equal(32768, stitcher.Height);
        Assert.Equal(Frame(0, 32768, 2, 3, 2).Pixels, stitcher.GetPixels());
        Assert.Equal(StitchStatus.LimitReached, stitcher.Append(Frame(40, 32760, 2, 3, 2)));
        Assert.Throws<ArgumentException>(() => new ScrollStitcher(Frame(0, 32769, width: 1)));
    }

    [Theory]
    [InlineData(1, 32768)]
    [InlineData(4000, 25000)]
    [InlineData(9999, 10001)]
    public void AT16_PixelLimit_UsesWholeRows(int width, int expected)
    {
        Assert.Equal(expected, ScrollStitcher.HeightLimit(width));
        Assert.True((long)width * expected <= ScrollStitcher.MaximumPixels);
        Assert.True(expected == ScrollStitcher.MaximumHeight || (long)width * (expected + 1) > ScrollStitcher.MaximumPixels);
    }

    [Fact]
    public void AT16_Preview_SamplesTheAssembledImageIncludingFixedBands()
    {
        var stitcher = new ScrollStitcher(Frame(0, header: 5, footer: 6));
        stitcher.Append(Frame(30, header: 5, footer: 6));
        var preview = stitcher.GetPreviewPixels(4, 400, out var width, out var height);
        var expected = Frame(0, 126, 5, 6);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                Assert.Equal(expected.Pixels.AsSpan(((y * 126 / height) * 7 + x * 7 / width) * 4, 4).ToArray(),
                    preview.AsSpan((y * width + x) * 4, 4).ToArray());
    }
}
