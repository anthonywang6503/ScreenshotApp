using ScreenshotApp.Editor.Rendering;

namespace ScreenshotApp.Tests;

public class PixelEffectsTests
{
    [Theory]
    [InlineData(1, 1, 2, 0, 0)]
    [InlineData(7, 5, 2, 0, 0)]
    [InlineData(7, 5, 3, 1, 2)]
    [InlineData(13, 9, 12, 5, 7)]
    [InlineData(65, 3, 64, 63, 1)]
    public void AT13_Mosaic_MatchesIndependentBlockAverage(int width, int height, int size, int offsetX, int offsetY)
    {
        var source = Pattern(width, height);
        var before = source.ToArray();
        Assert.Equal(ReferenceMosaic(source, width, height, size, offsetX, offsetY),
            PixelEffects.Mosaic(source, width, height, size, offsetX, offsetY));
        Assert.Equal(before, source);
    }

    [Theory]
    [InlineData(1, 1, 32)]
    [InlineData(1, 9, 1)]
    [InlineData(9, 1, 2)]
    [InlineData(7, 5, 1)]
    [InlineData(7, 5, 6)]
    [InlineData(17, 13, 32)]
    public void AT13_Blur_MatchesIndependentSixPassReference(int width, int height, int radius)
    {
        var source = Pattern(width, height);
        var before = source.ToArray();
        Assert.Equal(ReferenceBlur(source, width, height, radius), PixelEffects.Blur(source, width, height, radius));
        Assert.Equal(before, source);
    }

    [Fact]
    public void AT13_UniformColor_IsUnchangedAndOpaque()
    {
        var input = Enumerable.Range(0, 35).SelectMany(_ => new byte[] { 13, 99, 203, 0 }).ToArray();
        var expected = Enumerable.Range(0, 35).SelectMany(_ => new byte[] { 13, 99, 203, 255 }).ToArray();
        Assert.Equal(expected, PixelEffects.Mosaic(input, 7, 5, 64));
        Assert.Equal(expected, PixelEffects.Blur(input, 7, 5, 32));
    }

    [Fact]
    public void AT13_Mosaic_HalfAverageRoundsUp()
    {
        byte[] input = [0, 1, 2, 0, 1, 2, 3, 0];
        Assert.Equal(new byte[] { 1, 2, 3, 255, 1, 2, 3, 255 }, PixelEffects.Mosaic(input, 2, 1, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(65)]
    public void AT13_Mosaic_RejectsInvalidStrength(int strength) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelEffects.Mosaic(new byte[4], 1, 1, strength));

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    public void AT13_Blur_RejectsInvalidStrength(int strength) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelEffects.Blur(new byte[4], 1, 1, strength));

    internal static byte[] Pattern(int width, int height) => Enumerable.Range(0, width * height)
        .SelectMany(i => new byte[] { (byte)(i * 37), (byte)(i * 71), (byte)(i * 113), (byte)(i % 256) }).ToArray();

    internal static byte[] ReferenceMosaic(byte[] source, int width, int height, int size, int offsetX = 0, int offsetY = 0)
    {
        var output = new byte[source.Length];
        var blocks = Enumerable.Range(0, width * height).GroupBy(i => ((i % width + offsetX) / size, (i / width + offsetY) / size));
        foreach (var block in blocks)
        {
            for (var channel = 0; channel < 3; channel++)
            {
                var mean = (byte)Math.Round(block.Average(i => (double)source[i * 4 + channel]), MidpointRounding.AwayFromZero);
                foreach (var i in block)
                {
                    output[i * 4 + channel] = mean;
                    output[i * 4 + 3] = 255;
                }
            }
        }

        return output;
    }

    internal static byte[] ReferenceBlur(byte[] source, int width, int height, int radius)
    {
        var current = source.ToArray();
        for (var pass = 0; pass < 6; pass++)
        {
            var next = new byte[current.Length];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    for (var channel = 0; channel < 3; channel++)
                    {
                        var samples = new List<byte>();
                        for (var d = -radius; d <= radius; d++)
                        {
                            var xx = x + (pass % 2 == 0 ? d : 0);
                            var yy = y + (pass % 2 == 1 ? d : 0);
                            if (xx >= 0 && xx < width && yy >= 0 && yy < height)
                            {
                                samples.Add(current[(yy * width + xx) * 4 + channel]);
                            }
                        }

                        next[(y * width + x) * 4 + channel] = (byte)Math.Round(samples.Average(v => (double)v), MidpointRounding.AwayFromZero);
                    }

                    next[(y * width + x) * 4 + 3] = 255;
                }
            }

            current = next;
        }

        return current;
    }
}
