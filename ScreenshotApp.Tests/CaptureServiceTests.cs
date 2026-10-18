using System.Windows.Media;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

/// <summary>
/// 以真實的 GDI 擷取驗證輸出格式與資源釋放。只檢查尺寸、格式與 GDI 物件數，不保存也不比對畫面內容
/// （畫面內容的像素正確性由實機煙霧驗證以已知圖樣比對）。
/// </summary>
[Collection(GdiObjectCountCollection.Name)]
public class CaptureServiceTests
{
    [Fact]
    public void Capture_ReturnsFrozenOpaqueBitmapOfTheRequestedPhysicalSize()
    {
        StaRunner.Run(() =>
        {
            NativeTestHelpers.UsePerMonitorV2();
            var region = new PixelRect(0, 0, 64, 48);

            var image = CaptureService.Capture(region);

            Assert.Equal(64, image.PixelWidth);
            Assert.Equal(48, image.PixelHeight);
            Assert.Equal(PixelFormats.Bgr32, image.Format);
            Assert.Equal(96, image.DpiX);
            Assert.True(image.IsFrozen);
        });
    }

    [Fact]
    public void Capture_EveryMonitor_MatchesItsBounds()
    {
        StaRunner.Run(() =>
        {
            NativeTestHelpers.UsePerMonitorV2();

            foreach (var monitor in MonitorService.GetMonitors())
            {
                var image = CaptureService.Capture(monitor.Bounds);

                Assert.Equal(monitor.Bounds.Width, image.PixelWidth);
                Assert.Equal(monitor.Bounds.Height, image.PixelHeight);
            }
        });
    }

    [Fact]
    public void Capture_Repeatedly_DoesNotLeakGdiObjects()
    {
        StaRunner.Run(() =>
        {
            NativeTestHelpers.UsePerMonitorV2();
            var monitors = MonitorService.GetMonitors();
            CaptureAll(monitors);
            var before = NativeTestHelpers.GdiObjectCount();

            for (var i = 0; i < 20; i++)
            {
                CaptureAll(monitors);
            }

            Assert.True(NativeTestHelpers.GdiObjectCount() <= before);
        });
    }

    private static void CaptureAll(IReadOnlyList<MonitorInfo> monitors)
    {
        foreach (var monitor in monitors)
        {
            CaptureService.Capture(monitor.Bounds);
        }
    }
}
