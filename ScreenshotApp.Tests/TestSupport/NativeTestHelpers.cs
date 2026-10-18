using System.Runtime.InteropServices;

namespace ScreenshotApp.Tests.TestSupport;

/// <summary>
/// 量測 GDI 物件數量的測試集合。數量以整個處理程序計算，量測期間不能有其他測試並行建立或釋放 GDI 物件。
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GdiObjectCountCollection
{
    public const string Name = "GDI 物件數量";
}

internal static partial class NativeTestHelpers
{
    private const uint GR_GDIOBJECTS = 0;
    private const nint PerMonitorAwareV2 = -4;

    /// <summary>
    /// 目前處理程序持有的 GDI 物件數。先讓終結器跑完，再等到連續兩次讀數相同才回傳：其他測試遺留的 WPF 繪製資源
    /// 由終結器交給繪製執行緒後才實際刪除，若立即讀取，這些物件可能在量測途中才被釋放。
    /// </summary>
    public static int GdiObjectCount()
    {
        var previous = -1;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var count = (int)GetGuiResources(GetCurrentProcess(), GR_GDIOBJECTS);
            if (count == previous)
            {
                return count;
            }

            previous = count;
            Thread.Sleep(25);
        }

        return previous;
    }

    /// <summary>
    /// 讓目前執行緒與應用程式一樣以 Per-Monitor V2 執行，取得未經 DPI 虛擬化的實體像素。
    /// 只應在測試專用的執行緒上呼叫，以免影響其他測試。
    /// </summary>
    public static void UsePerMonitorV2() => SetThreadDpiAwarenessContext(PerMonitorAwareV2);

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("user32.dll")]
    private static partial uint GetGuiResources(nint hProcess, uint uiFlags);

    [LibraryImport("user32.dll")]
    private static partial nint SetThreadDpiAwarenessContext(nint dpiContext);
}
