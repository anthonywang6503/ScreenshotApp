using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Editor.Rendering;

/// <summary>僅供 UI 執行緒使用；弱參照讓已關閉文件的原圖與效果一起回收。</summary>
internal sealed class EffectBitmapCache
{
    private const int MaximumEntries = 8;
    private const long MaximumBytes = 64 * 1024 * 1024;
    private static readonly ConditionalWeakTable<BitmapSource, EffectBitmapCache> Sources = new();
    private readonly LinkedList<Entry> _entries = new();
    private long _bytes;

    public static BitmapSource? Get(BitmapSource source, EffectAnnotation effect)
    {
        if (!source.IsFrozen)
        {
            throw new ArgumentException("效果取樣原圖必須已凍結。", nameof(source));
        }

        return Sources.GetOrCreateValue(source).GetOrCreate(source, effect);
    }

    private BitmapSource? GetOrCreate(BitmapSource source, EffectAnnotation effect)
    {
        var mosaic = effect switch
        {
            MosaicAnnotation => true,
            BlurAnnotation => false,
            _ => throw new ArgumentException("不支援的效果種類。", nameof(effect)),
        };
        var key = new Key(effect.Rect, mosaic, effect.Strength);
        for (var node = _entries.First; node is not null; node = node.Next)
        {
            if (node.Value.Key == key)
            {
                _entries.Remove(node);
                _entries.AddFirst(node);
                return node.Value.Image;
            }
        }

        var rect = effect.Rect.Intersect(new PixelRect(0, 0, source.PixelWidth, source.PixelHeight));
        if (rect.IsEmpty)
        {
            return null;
        }

        var input = new byte[checked(rect.Width * rect.Height * 4)];
        // 保留未預乘 RGB，alpha 不影響不透明效果的平均值。
        var readable = source.Format == PixelFormats.Bgr32 || source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        readable.CopyPixels(new Int32Rect(rect.X, rect.Y, rect.Width, rect.Height), input, rect.Width * 4, 0);
        var pixels = mosaic
            ? PixelEffects.Mosaic(input, rect.Width, rect.Height, effect.Strength, rect.X - effect.Rect.X, rect.Y - effect.Rect.Y)
            : PixelEffects.Blur(input, rect.Width, rect.Height, effect.Strength);
        var image = BitmapSource.Create(rect.Width, rect.Height, 96, 96, PixelFormats.Bgr32, null, pixels, rect.Width * 4);
        image.Freeze();
        if (pixels.LongLength <= MaximumBytes)
        {
            while (_entries.Count >= MaximumEntries || _bytes + pixels.LongLength > MaximumBytes)
            {
                _bytes -= _entries.Last!.Value.Bytes;
                _entries.RemoveLast();
            }

            _entries.AddFirst(new Entry(key, image, pixels.LongLength));
            _bytes += pixels.LongLength;
        }

        return image;
    }

    private readonly record struct Key(PixelRect Rect, bool Mosaic, int Strength);

    private sealed record Entry(Key Key, BitmapSource Image, long Bytes);
}
