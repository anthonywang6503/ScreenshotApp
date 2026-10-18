using System.Runtime.InteropServices;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Resources;

namespace ScreenshotApp.Platform;

/// <summary>一個顯示器。<see cref="Bounds"/> 為桌面實體像素：主螢幕左上角為原點，其他顯示器可為負值。</summary>
internal sealed record MonitorInfo(int Index, PixelRect Bounds);

/// <summary>列舉目前的顯示器。處理程序為 Per-Monitor V2，取得的邊界是未經 DPI 虛擬化的實體像素。</summary>
internal static class MonitorService
{
    public static unsafe IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var bounds = new List<PixelRect>();
        var handle = GCHandle.Alloc(bounds);
        try
        {
            if (!NativeMethods.EnumDisplayMonitors(0, 0, &CollectMonitor, GCHandle.ToIntPtr(handle)) || bounds.Count == 0)
            {
                throw new CaptureException(Strings.Get("Capture_NoMonitor"));
            }
        }
        finally
        {
            handle.Free();
        }

        return bounds.Select((rect, index) => new MonitorInfo(index, rect)).ToList();
    }

    // 沒有指定 DC 時，第三個參數就是該顯示器在桌面上的矩形。
    [UnmanagedCallersOnly]
    private static unsafe int CollectMonitor(nint monitor, nint hdc, NativeMethods.RECT* rect, nint data)
    {
        var bounds = (List<PixelRect>)GCHandle.FromIntPtr(data).Target!;
        bounds.Add(new PixelRect(rect->Left, rect->Top, rect->Right - rect->Left, rect->Bottom - rect->Top));
        return 1;
    }
}
