using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;

namespace ScreenshotApp.Capture;

/// <summary>選取結果：顯示器序號與該顯示器本地實體像素中的選取範圍。</summary>
internal readonly record struct SelectionResult(int MonitorIndex, PixelRect Rect);

/// <summary>
/// 一次拖曳選取的幾何（桌面實體像素）。按下的位置決定起始顯示器，之後的游標位置一律夾取在該顯示器內。
/// 選取範圍包含起點與游標所在的兩個像素，因此可以選到顯示器最後一行與最後一列；
/// 游標仍在起點像素時代表尚未拖曳，沒有選取範圍。
/// </summary>
internal sealed class DragSelection
{
    private readonly PixelPoint _start;

    private DragSelection(MonitorInfo monitor, PixelPoint start)
    {
        Monitor = monitor;
        _start = start;
    }

    public MonitorInfo Monitor { get; }

    /// <summary>在 <paramref name="desktopPoint"/> 按下時開始選取；該點不在任何顯示器內時回傳 null。</summary>
    public static DragSelection? Begin(IReadOnlyList<MonitorInfo> monitors, PixelPoint desktopPoint)
    {
        var monitor = monitors.FirstOrDefault(candidate => candidate.Bounds.Contains(desktopPoint));
        return monitor is null ? null : new DragSelection(monitor, desktopPoint);
    }

    /// <summary>游標位於 <paramref name="desktopPoint"/> 時的選取範圍（顯示器本地實體像素）；尚未離開起點像素時為 null。</summary>
    public PixelRect? RectTo(PixelPoint desktopPoint)
    {
        var end = Monitor.Bounds.Clamp(desktopPoint);
        if (end == _start)
        {
            return null;
        }

        var desktopRect = PixelRect.FromCorners(
            new PixelPoint(Math.Min(_start.X, end.X), Math.Min(_start.Y, end.Y)),
            new PixelPoint(Math.Max(_start.X, end.X) + 1, Math.Max(_start.Y, end.Y) + 1));
        return CoordinateMapper.DesktopToMonitorLocal(desktopRect, Monitor.Bounds);
    }

    /// <summary>在 <paramref name="desktopPoint"/> 放開時的選取結果；沒有拖曳（零尺寸）時為 null。</summary>
    public SelectionResult? Complete(PixelPoint desktopPoint) =>
        RectTo(desktopPoint) is { } rect ? new SelectionResult(Monitor.Index, rect) : null;
}
