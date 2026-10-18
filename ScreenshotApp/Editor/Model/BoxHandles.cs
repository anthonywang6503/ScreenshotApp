using System.Windows;

namespace ScreenshotApp.Editor.Model;

/// <summary>矩形範圍的八個控制點與以控制點調整範圍；矩形框、橢圓與裁切範圍共用。</summary>
internal static class BoxHandles
{
    /// <summary>四角與四邊中點，位於範圍的像素格線上。</summary>
    public static IReadOnlyList<AnnotationHandle> Of(PixelRect rect)
    {
        var middleX = rect.X + rect.Width / 2.0;
        var middleY = rect.Y + rect.Height / 2.0;
        return
        [
            new(HandleKind.TopLeft, new Point(rect.X, rect.Y)),
            new(HandleKind.Top, new Point(middleX, rect.Y)),
            new(HandleKind.TopRight, new Point(rect.Right, rect.Y)),
            new(HandleKind.Right, new Point(rect.Right, middleY)),
            new(HandleKind.BottomRight, new Point(rect.Right, rect.Bottom)),
            new(HandleKind.Bottom, new Point(middleX, rect.Bottom)),
            new(HandleKind.BottomLeft, new Point(rect.X, rect.Bottom)),
            new(HandleKind.Left, new Point(rect.X, middleY)),
        ];
    }

    /// <summary>包含兩個像素及其間所有像素的矩形；兩點順序不拘，至少 1×1。</summary>
    public static PixelRect Spanning(PixelPoint a, PixelPoint b) =>
        PixelRect.FromCorners(
            new PixelPoint(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)),
            new PixelPoint(Math.Max(a.X, b.X) + 1, Math.Max(a.Y, b.Y) + 1));

    /// <summary>
    /// 把控制點拖曳 (<paramref name="dx"/>, <paramref name="dy"/>) 個像素後的範圍：以範圍最外圍的像素移動對應的邊，
    /// 拖過對邊時自動翻轉，至少保留 1×1。<paramref name="limit"/> 不為 null 時，移動的邊限制在其範圍內。
    /// </summary>
    public static PixelRect Resize(PixelRect rect, HandleKind handle, int dx, int dy, PixelRect? limit = null)
    {
        var left = rect.X;
        var right = rect.Right - 1;
        var top = rect.Y;
        var bottom = rect.Bottom - 1;

        if (handle is HandleKind.TopLeft or HandleKind.Left or HandleKind.BottomLeft)
        {
            left = Limit(left + dx, limit?.X, limit?.Right);
        }

        if (handle is HandleKind.TopRight or HandleKind.Right or HandleKind.BottomRight)
        {
            right = Limit(right + dx, limit?.X, limit?.Right);
        }

        if (handle is HandleKind.TopLeft or HandleKind.Top or HandleKind.TopRight)
        {
            top = Limit(top + dy, limit?.Y, limit?.Bottom);
        }

        if (handle is HandleKind.BottomLeft or HandleKind.Bottom or HandleKind.BottomRight)
        {
            bottom = Limit(bottom + dy, limit?.Y, limit?.Bottom);
        }

        return Spanning(new PixelPoint(left, top), new PixelPoint(right, bottom));
    }

    // 限制在 [min, maxExclusive - 1]；沒有限制時原值不變。
    private static int Limit(int value, int? min, int? maxExclusive) =>
        min is { } low && maxExclusive is { } high ? Math.Clamp(value, low, high - 1) : value;
}
