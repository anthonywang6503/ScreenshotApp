using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenshotApp.Capture;

/// <summary>由工作階段獨佔的 Bgr32 像素；未定義的第四個位元組正規化後才比較。</summary>
internal sealed class ScrollFrame
{
    public int Width { get; }
    public int Height { get; }
    public int Stride => Width * 4;
    public byte[] Pixels { get; }
    public ulong[] Rows { get; }

    public ScrollFrame(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0 || pixels.LongLength != (long)width * height * 4)
            throw new ArgumentException("影格尺寸與像素不一致。");
        Width = width;
        Height = height;
        Pixels = pixels;
        Rows = new ulong[height];
        for (var y = 0; y < height; y++)
        {
            var hash = 14695981039346656037UL;
            for (var x = 0; x < Stride; x += 4)
            {
                var i = y * Stride + x;
                pixels[i + 3] = 0;
                for (var c = 0; c < 3; c++) hash = unchecked((hash ^ pixels[i + c]) * 1099511628211UL);
            }
            Rows[y] = hash;
        }
    }

    public static ScrollFrame FromBitmap(BitmapSource source)
    {
        if (source.Format != PixelFormats.Bgr32)
            source = new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0);
        var pixels = new byte[checked(source.PixelWidth * source.PixelHeight * 4)];
        source.CopyPixels(pixels, source.PixelWidth * 4, 0);
        return new ScrollFrame(source.PixelWidth, source.PixelHeight, pixels);
    }

    public bool SamePixels(ScrollFrame other) => Width == other.Width && Height == other.Height
        && Rows.AsSpan().SequenceEqual(other.Rows) && Pixels.AsSpan().SequenceEqual(other.Pixels);

    public bool SameRow(int row, ScrollFrame other, int otherRow) => Rows[row] == other.Rows[otherRow]
        && Pixels.AsSpan(row * Stride, Stride).SequenceEqual(other.Pixels.AsSpan(otherRow * Stride, Stride));
}
