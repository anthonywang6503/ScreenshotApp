using System.Windows;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Capture;

/// <summary>
/// 座標空間之間換算的唯一位置，全部為純函式。
/// <list type="bullet">
/// <item><term>DesktopPhysical</term><description>主螢幕左上為原點、可為負值的實體像素，用於顯示器邊界與游標。</description></item>
/// <item><term>MonitorLocal</term><description>該顯示器左上為原點的實體像素，用於選取結果與逐顯示器快照；新截圖的圖片像素與此相同。</description></item>
/// <item><term>Image</term><description>原始圖片左上為原點的圖片像素，文件模型（裁切、標註）的唯一座標。</description></item>
/// <item><term>ViewDip</term><description>WPF DIP。選取覆蓋層的用戶區與顯示器完全重合，比例即該顯示器的 DPI 縮放（例如 1.75）；
/// 編輯畫布以 <see cref="ImageViewTransform"/> 描述（比例為縮放 ÷ DPI 縮放，加上圖片原點的位置）。</description></item>
/// </list>
/// </summary>
internal static class CoordinateMapper
{
    // 浮點換整數像素時容許的誤差，遠小於任何縮放比例下一個實體像素對應的圖片長度。
    private const double PixelEpsilon = 1e-6;

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

    /// <summary>
    /// 編輯畫布的縮放比例（每個圖片像素占幾個 DIP）。<paramref name="zoom"/> 為每個圖片像素占幾個實體像素：
    /// 100% 即 1 圖片像素 = 1 實體像素，不因螢幕的 DPI 縮放而放大。
    /// </summary>
    public static double ImageToViewScale(double zoom, double dpiScale)
    {
        if (!double.IsFinite(zoom) || zoom <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom), zoom, "縮放比例必須是大於 0 的有限數值。");
        }

        ValidateDpiScale(dpiScale);
        return zoom / dpiScale;
    }

    public static Point ImageToViewDip(Point image, ImageViewTransform view) =>
        new(view.Origin.X + image.X * view.Scale, view.Origin.Y + image.Y * view.Scale);

    public static Point ViewDipToImage(Point dip, ImageViewTransform view) =>
        new((dip.X - view.Origin.X) / view.Scale, (dip.Y - view.Origin.Y) / view.Scale);

    /// <summary>螢幕上的長度（DIP）換成圖片像素長度，例如以螢幕尺寸定義的命中容差與控制點大小。</summary>
    public static double ViewLengthToImage(double dip, ImageViewTransform view) => dip / view.Scale;

    /// <summary>
    /// 圖片座標所在的像素（無條件捨去）。先加上極小的容許值：DIP 往返換算的浮點誤差可能讓 5 變成 4.9999999999，
    /// 加上後 100% 縮放時游標所在的實體像素會精確對應同一個圖片像素。
    /// </summary>
    public static PixelPoint ImageToPixel(Point image) =>
        new((int)Math.Floor(image.X + PixelEpsilon), (int)Math.Floor(image.Y + PixelEpsilon));

    /// <summary>
    /// 縮放後要讓圖片上的點 <paramref name="image"/> 仍位於可視區域的 <paramref name="viewportPoint"/>，捲動位移應設為多少。
    /// <paramref name="contentOrigin"/> 為縮放後圖片原點在捲動內容中的位置（DIP），<paramref name="scale"/> 為縮放後的比例。
    /// </summary>
    public static Point ScrollOffsetToKeep(Point image, double scale, Point contentOrigin, Point viewportPoint)
    {
        var content = ImageToViewDip(image, new ImageViewTransform(scale, contentOrigin));
        return new Point(content.X - viewportPoint.X, content.Y - viewportPoint.Y);
    }

    private static void ValidateDpiScale(double dpiScale)
    {
        if (!double.IsFinite(dpiScale) || dpiScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpiScale), dpiScale, "DPI 縮放比例必須是大於 0 的有限數值。");
        }
    }
}

/// <summary>
/// 編輯畫布的一次圖片像素 → DIP 轉換：<c>dip = Origin + image × Scale</c>。Scale 由 <see cref="CoordinateMapper.ImageToViewScale"/> 計算，
/// Origin 為圖片原點在參考元素中的 DIP 位置（由 WPF 版面取得）。
/// </summary>
internal readonly record struct ImageViewTransform(double Scale, Point Origin);
