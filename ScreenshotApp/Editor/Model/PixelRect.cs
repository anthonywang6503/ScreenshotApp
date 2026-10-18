namespace ScreenshotApp.Editor.Model;

/// <summary>
/// 整數像素矩形，採左上包含、右下不包含的半開區間：涵蓋 X 到 <see cref="Right"/> - 1、Y 到 <see cref="Bottom"/> - 1 的像素。
/// 屬性只有 getter，<c>with</c> 運算式無法繞過建構子對寬高的驗證；<c>default</c> 為原點的空矩形。
/// </summary>
internal readonly record struct PixelRect
{
    public PixelRect(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>右側邊界，本身不屬於矩形。</summary>
    public int Right => X + Width;

    /// <summary>下側邊界，本身不屬於矩形。</summary>
    public int Bottom => Y + Height;

    public bool IsEmpty => Width == 0 || Height == 0;

    /// <summary>
    /// 由像素格線上的兩個角點建立矩形。兩點順序不拘，任何拖曳方向都得到相同的矩形；兩點 X 或 Y 相同時為空矩形。
    /// </summary>
    public static PixelRect FromCorners(PixelPoint a, PixelPoint b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        return new PixelRect(left, top, Math.Max(a.X, b.X) - left, Math.Max(a.Y, b.Y) - top);
    }

    /// <summary>像素是否位於矩形內；空矩形不包含任何像素。</summary>
    public bool Contains(PixelPoint point) =>
        point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    /// <summary><paramref name="other"/> 的四個邊是否都落在此矩形的邊界之內（含邊界）。</summary>
    public bool Contains(PixelRect other) =>
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    /// <summary>兩個矩形的重疊區域；不重疊或只有邊相接時為空矩形。</summary>
    public PixelRect Intersect(PixelRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return new PixelRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    public PixelRect Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);

    /// <summary>
    /// 矩形內最接近 <paramref name="point"/> 的像素：X 限制在 X 到 <see cref="Right"/> - 1、Y 限制在 Y 到 <see cref="Bottom"/> - 1。
    /// </summary>
    /// <exception cref="InvalidOperationException">矩形為空，沒有任何像素可對應。</exception>
    public PixelPoint Clamp(PixelPoint point)
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("空矩形沒有可對應的像素。");
        }

        return new PixelPoint(Math.Clamp(point.X, X, Right - 1), Math.Clamp(point.Y, Y, Bottom - 1));
    }
}
