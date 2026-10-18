using System.Runtime.InteropServices;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

/// <summary>以實際的顯示器配置驗證列舉結果，並與 Windows 系統計量交叉比對（以 Per-Monitor V2 取得實體像素）。</summary>
public partial class MonitorServiceTests
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;
    private const int SM_CMONITORS = 80;

    [Fact]
    public void GetMonitors_MatchesTheSystemMonitorCountWithSequentialIndexes()
    {
        StaRunner.Run(() =>
        {
            NativeTestHelpers.UsePerMonitorV2();

            var monitors = MonitorService.GetMonitors();

            Assert.Equal(GetSystemMetrics(SM_CMONITORS), monitors.Count);
            Assert.Equal(Enumerable.Range(0, monitors.Count), monitors.Select(monitor => monitor.Index));
            Assert.All(monitors, monitor => Assert.False(monitor.Bounds.IsEmpty));
        });
    }

    [Fact]
    public void GetMonitors_PrimaryMonitorStartsAtTheDesktopOriginWithPhysicalSize()
    {
        StaRunner.Run(() =>
        {
            NativeTestHelpers.UsePerMonitorV2();

            var primary = Assert.Single(MonitorService.GetMonitors(), monitor => monitor.Bounds.Contains(new PixelPoint(0, 0)));

            Assert.Equal(new PixelRect(0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN)), primary.Bounds);
        });
    }

    [Fact]
    public void GetMonitors_DoNotOverlapAndSpanTheVirtualScreen()
    {
        StaRunner.Run(() =>
        {
            NativeTestHelpers.UsePerMonitorV2();

            var bounds = MonitorService.GetMonitors().Select(monitor => monitor.Bounds).ToList();

            for (var i = 0; i < bounds.Count; i++)
            {
                for (var j = i + 1; j < bounds.Count; j++)
                {
                    Assert.True(bounds[i].Intersect(bounds[j]).IsEmpty);
                }
            }

            var left = bounds.Min(rect => rect.X);
            var top = bounds.Min(rect => rect.Y);
            var virtualScreen = new PixelRect(
                GetSystemMetrics(SM_XVIRTUALSCREEN),
                GetSystemMetrics(SM_YVIRTUALSCREEN),
                GetSystemMetrics(SM_CXVIRTUALSCREEN),
                GetSystemMetrics(SM_CYVIRTUALSCREEN));
            Assert.Equal(virtualScreen, new PixelRect(left, top, bounds.Max(rect => rect.Right) - left, bounds.Max(rect => rect.Bottom) - top));
        });
    }

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);
}
