using System.Windows;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;

namespace ScreenshotApp.Capture;

/// <summary>單一顯示器的快照；影像像素與該顯示器的本地實體像素一一對應。</summary>
internal sealed record MonitorSnapshot(MonitorInfo Monitor, BitmapSource Image);

/// <summary>同一時刻全部顯示器的快照。</summary>
internal sealed class DesktopSnapshot
{
    public DesktopSnapshot(IReadOnlyList<MonitorSnapshot> monitors)
    {
        Monitors = monitors;
    }

    public IReadOnlyList<MonitorSnapshot> Monitors { get; }

    /// <summary>
    /// 複製出選取範圍，成為獨立的凍結 bitmap。不使用 <see cref="CroppedBitmap"/>，
    /// 以免一小塊裁切結果一直留住整張顯示器快照的記憶體。
    /// </summary>
    public BitmapSource Crop(SelectionResult selection)
    {
        var source = Monitors[selection.MonitorIndex].Image;
        var rect = selection.Rect;
        if (rect.IsEmpty || !new PixelRect(0, 0, source.PixelWidth, source.PixelHeight).Contains(rect))
        {
            throw new ArgumentOutOfRangeException(nameof(selection), "選取範圍必須位於該顯示器快照之內，且不得為空。");
        }

        var stride = rect.Width * ((source.Format.BitsPerPixel + 7) / 8);
        var pixels = new byte[stride * rect.Height];
        source.CopyPixels(new Int32Rect(rect.X, rect.Y, rect.Width, rect.Height), pixels, stride, 0);
        var image = BitmapSource.Create(rect.Width, rect.Height, 96, 96, source.Format, null, pixels, stride);
        image.Freeze();
        return image;
    }
}
