using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Capture;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;

namespace ScreenshotApp.Tests;

public class MonitorSnapshotTests
{
    [Theory]
    [InlineData(0, 0, "#001000")]
    [InlineData(2, 1, "#0211AB")]
    public void AT12_ColorCode_UsesLocalPixelsUppercaseAndIgnoresUndefinedAlpha(int x, int y, string expected)
    {
        Assert.Equal(expected, Snapshot().ColorCodeAt(new PixelPoint(x, y)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    public void AT11_SampleCorners_PadWithGrayWithoutMovingCenter(int x, int y)
    {
        var snapshot = Snapshot();
        var sample = snapshot.Sample(new PixelRect(x - 2, y - 2, 5, 5));
        Assert.True(sample.IsFrozen);
        Assert.Equal(PixelFormats.Bgr32, sample.Format);
        Assert.Equal(5, sample.PixelWidth);
        Assert.Equal(5, sample.PixelHeight);
        var pixels = new byte[5 * 5 * 4];
        sample.CopyPixels(pixels, 20, 0);
        for (var row = 0; row < 5; row++)
        {
            for (var col = 0; col < 5; col++)
            {
                var sx = x - 2 + col;
                var sy = y - 2 + row;
                var expected = sx >= 0 && sx < 3 && sy >= 0 && sy < 2
                    ? new byte[] { (byte)(sx == 0 ? 0 : 0xAB), (byte)(sy + 16), (byte)sx }
                    : new byte[] { 128, 128, 128 };
                var offset = (row * 5 + col) * 4;
                Assert.Equal(expected, pixels[offset..(offset + 3)]);
            }
        }
    }

    [Fact]
    public void AT12_OutsideSnapshot_ThrowsInsteadOfPickingEdge()
    {
        Assert.NotNull(Record.Exception(() => Snapshot().ColorCodeAt(new PixelPoint(-1, 0))));
        Assert.NotNull(Record.Exception(() => Snapshot().ColorCodeAt(new PixelPoint(3, 0))));
    }

    private static MonitorSnapshot Snapshot()
    {
        var pixels = new byte[24];
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                var i = (y * 3 + x) * 4;
                pixels[i] = (byte)(x == 0 ? 0 : 0xAB);
                pixels[i + 1] = (byte)(y + 16);
                pixels[i + 2] = (byte)x;
            }
        }

        var image = BitmapSource.Create(3, 2, 96, 96, PixelFormats.Bgr32, null, pixels, 12);
        image.Freeze();
        return new MonitorSnapshot(new MonitorInfo(0, new PixelRect(-1920, -1080, 3, 2)), image);
    }
}
