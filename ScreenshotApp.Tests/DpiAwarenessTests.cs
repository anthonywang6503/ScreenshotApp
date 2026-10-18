using System.Runtime.InteropServices;
using ScreenshotApp.Platform;

namespace ScreenshotApp.Tests;

public partial class DpiAwarenessTests
{
    private const nint Unaware = -1;
    private const nint PerMonitorAwareV2 = -4;

    [Fact]
    public void IsPerMonitorV2_ReflectsTheThreadAwarenessContext()
    {
        bool? whenPerMonitorV2 = null;
        bool? whenUnaware = null;

        // 在專用執行緒切換 DPI 感知，避免影響其他測試。
        var thread = new Thread(() =>
        {
            SetThreadDpiAwarenessContext(PerMonitorAwareV2);
            whenPerMonitorV2 = DpiAwareness.IsPerMonitorV2();
            SetThreadDpiAwarenessContext(Unaware);
            whenUnaware = DpiAwareness.IsPerMonitorV2();
        });
        thread.Start();
        thread.Join();

        Assert.True(whenPerMonitorV2);
        Assert.False(whenUnaware);
    }

    [LibraryImport("user32.dll")]
    private static partial nint SetThreadDpiAwarenessContext(nint dpiContext);
}
