using System.Windows;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Capture;

/// <summary>
/// 座標空間之間換算的唯一位置，全部為純函式。
/// <list type="bullet">
/// <item><term>DesktopPhysical</term><description>主螢幕左上為原點、可為負值的實體像素，用於顯示器邊界與游標。</description></item>
/// <item><term>MonitorLocal</term><description>該顯示器左上為原點的實體像素，用於選取結果與逐顯示器快照；新截圖的圖片像素與此相同。</description></item>
/// <item><term>ViewDip</term><description>視窗用戶區左上為原點的 WPF DIP。目前只有選取覆蓋層使用，其用戶區與顯示器完全重合，比例即該顯示器的 DPI 縮放（例如 1.75）。</description></item>
/// </list>
/// </summary>
internal static class CoordinateMapper
{
    /// <summary>減去顯示器左上角，不做範圍限制；顯示器外的點會得到超出該顯示器的本地座標。</summary>
    public static PixelPoint DesktopToMonitorLocal(PixelPoint desktop, PixelRect monitorBounds) =>
        new(desktop.X - monitorBounds.X, desktop.Y - monitorBounds.Y);

    public static PixelPoint MonitorLocalToDesktop(PixelPoint local, PixelRect monitorBounds) =>
        new(local.X + monitorBounds.X, local.Y + monitorBounds.Y);

    public static PixelRect DesktopToMonitorLocal(PixelRect desktop, PixelRect monitorBounds) =>
        desktop.Offset(-monitorBounds.X, -monitorBounds.Y);

    public static PixelRect MonitorLocalToDesktop(PixelRect local, PixelRect monitorBounds) =>
        local.Offset(monitorBounds.X, monitorBounds.Y);

    public static Point MonitorLocalToViewDip(PixelPoint local, double dpiScale)
    {
        ValidateDpiScale(dpiScale);
        return new Point(local.X / dpiScale, local.Y / dpiScale);
    }

    /// <summary>四個邊各自換算，讓矩形的邊與同一條像素格線換算出的 DIP 位置一致。</summary>
    public static Rect MonitorLocalToViewDip(PixelRect local, double dpiScale)
    {
        var topLeft = MonitorLocalToViewDip(new PixelPoint(local.X, local.Y), dpiScale);
        var bottomRight = MonitorLocalToViewDip(new PixelPoint(local.Right, local.Bottom), dpiScale);
        return new Rect(topLeft, bottomRight);
    }

    /// <summary>
    /// 四捨五入到最近的像素格線角點，中點遠離零。由整數像素換出的 DIP 乘回縮放比例後，
    /// 浮點誤差遠小於半個像素，因此往返後必定回到原本的整數像素。
    /// </summary>
    public static PixelPoint ViewDipToMonitorLocal(Point dip, double dpiScale)
    {
        ValidateDpiScale(dpiScale);
        return new PixelPoint(
            (int)Math.Round(dip.X * dpiScale, MidpointRounding.AwayFromZero),
            (int)Math.Round(dip.Y * dpiScale, MidpointRounding.AwayFromZero));
    }

    private static void ValidateDpiScale(double dpiScale)
    {
        if (!double.IsFinite(dpiScale) || dpiScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpiScale), dpiScale, "DPI 縮放比例必須是大於 0 的有限數值。");
        }
    }
}
