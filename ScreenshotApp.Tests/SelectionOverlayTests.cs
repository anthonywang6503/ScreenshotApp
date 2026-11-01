using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Capture;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

[Collection(GdiObjectCountCollection.Name)]
public class SelectionOverlayTests
{
    [Theory]
    [InlineData(100, 100)]
    [InlineData(0, 0)]
    [InlineData(799, 0)]
    [InlineData(0, 599)]
    [InlineData(799, 599)]
    public void AT11_HiddenOverlay_RendersEveryMagnifiedPixelWithoutInterpolation(int cursorX, int cursorY)
    {
        StaRunner.Run(() =>
        {
            var pixels = new byte[800 * 600 * 4];
            for (var y = 0; y < 600; y++)
            {
                for (var x = 0; x < 800; x++)
                {
                    var offset = (y * 800 + x) * 4;
                    pixels[offset] = (byte)(x ^ y);
                    pixels[offset + 1] = (byte)y;
                    pixels[offset + 2] = (byte)x;
                }
            }

            var image = BitmapSource.Create(800, 600, 96, 96, PixelFormats.Bgr32, null, pixels, 3200);
            image.Freeze();
            using var overlay = new SelectionOverlay(new MonitorSnapshot(new MonitorInfo(0, new PixelRect(0, 0, 800, 600)), image));
            var panel = Assert.IsType<Canvas>(overlay.FindName("MagnifierPanel"));
            Assert.Equal(Visibility.Collapsed, panel.Visibility);
            overlay.ShowSelection(new PixelRect(10, 20, 80, 40));
            overlay.ShowMagnifier(new PixelPoint(cursorX, cursorY));
            var dpi = VisualTreeHelper.GetDpi(overlay).DpiScaleX;
            var size = CoordinateMapper.MonitorLocalToViewDip(new PixelRect(0, 0, 800, 600), dpi).Size;
            overlay.Measure(size);
            overlay.Arrange(new Rect(size));
            overlay.UpdateLayout();
            Assert.Equal(Visibility.Visible, panel.Visibility);
            var label = Assert.IsType<TextBlock>(overlay.FindName("MagnifierText"));
            Assert.Equal($"({cursorX}, {cursorY}) px\n#{(byte)cursorX:X2}{(byte)cursorY:X2}{(byte)(cursorX ^ cursorY):X2}", label.Text);
            Assert.True(panel.ActualWidth > 0);
            var origin = CoordinateMapper.ViewDipToMonitorLocal(new Point(Canvas.GetLeft(panel), Canvas.GetTop(panel)), dpi);
            var output = new RenderTargetBitmap(800, 600, 96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
            output.Render(overlay);
            var actual = new byte[180 * 180 * 4];
            output.CopyPixels(new Int32Rect(origin.X, origin.Y, 180, 180), actual, 720, 0);
            for (var y = 0; y < 180; y++)
            {
                for (var x = 0; x < 180; x++)
                {
                    var sx = cursorX - 7 + x / 12;
                    var sy = cursorY - 7 + y / 12;
                    byte[] expected = sx >= 0 && sx < 800 && sy >= 0 && sy < 600
                        ? [(byte)(sx ^ sy), (byte)sy, (byte)sx] : [128, 128, 128];
                    if (x >= 84 && x < 96 && y >= 84 && y < 96)
                    {
                        if (x == 84 || x == 95 || y == 84 || y == 95) expected = [0, 0, 0];
                        else if (x == 85 || x == 94 || y == 85 || y == 94) expected = [255, 255, 255];
                    }

                    var offset = (y * 180 + x) * 4;
                    Assert.Equal(expected, actual[offset..(offset + 3)]);
                    Assert.Equal(255, actual[offset + 3]);
                }
            }

            overlay.HideMagnifier();
            Assert.Equal(Visibility.Collapsed, panel.Visibility);
            Assert.Equal(Visibility.Visible, Assert.IsType<Border>(overlay.FindName("SizeLabel")).Visibility);
        });
    }
}
