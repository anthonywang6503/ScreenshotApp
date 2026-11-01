using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;

namespace ScreenshotApp.Capture;

/// <summary>單一顯示器的快照；影像像素與該顯示器的本地實體像素一一對應。</summary>
internal sealed record MonitorSnapshot(MonitorInfo Monitor, BitmapSource Image)
{
    public string ColorCodeAt(PixelPoint local)
    {
        var pixel = new byte[4];
        Image.CopyPixels(new Int32Rect(local.X, local.Y, 1, 1), pixel, 4, 0);
        return $"#{pixel[2]:X2}{pixel[1]:X2}{pixel[0]:X2}";
    }

    /// <summary>保留完整取樣格；顯示器外填灰色，不複製邊緣像素。</summary>
    public BitmapSource Sample(PixelRect sample)
    {
        var stride = sample.Width * 4;
        var pixels = new byte[stride * sample.Height];
        Array.Fill(pixels, (byte)128);
        var inside = sample.Intersect(new PixelRect(0, 0, Image.PixelWidth, Image.PixelHeight));
        if (!inside.IsEmpty)
        {
            Image.CopyPixels(new Int32Rect(inside.X, inside.Y, inside.Width, inside.Height), pixels, stride,
                (inside.Y - sample.Y) * stride + (inside.X - sample.X) * 4);
        }

        var image = BitmapSource.Create(sample.Width, sample.Height, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
        image.Freeze();
        return image;
    }
}

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
