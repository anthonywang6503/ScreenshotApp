using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Resources;

namespace ScreenshotApp.Export;

/// <summary>
/// 開啟圖片的結果：成功時 Image 為凍結的 Bgr32 圖片、Error 為 null；
/// 失敗時 Image 為 null、Error 為可直接顯示給使用者的繁體中文原因。
/// </summary>
internal sealed record ImageLoadResult(BitmapSource? Image, string? Error);

/// <summary>
/// 開啟既有的 PNG、JPEG、BMP 圖片，轉成與截圖相同的不透明 Bgr32 底圖。格式依檔案內容判斷，不看副檔名；
/// 只取第一個影格，不套用色彩描述檔與 EXIF 方向，透明部分以白色背景合成，輸出固定為 96 DPI。
/// 所有失敗都以結果回報而不拋出例外；不寫日誌，也不把圖片內容寫到磁碟。結果已凍結，可交給其他執行緒使用。
/// </summary>
internal static class ImageFileLoader
{
    /// <summary>單張圖片的像素上限（寬 × 高）。</summary>
    public const long MaxPixels = 100_000_000;

    public static ImageLoadResult Load(string path)
    {
        FileStream stream;
        try
        {
            // 其他程式以可寫入方式開著同一個檔案時仍可讀取；讀完立即關閉，不妨礙之後刪除或改名。
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Failure(Strings.Get("ImageLoad_FileNotFound"));
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(Strings.Get("ImageLoad_AccessDenied"));
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            // 檔案被其他程式獨占、路徑過長或格式無效等；找不到檔案的兩種 IOException 已在前面處理。
            return Failure(Strings.Format("ImageLoad_ReadFailed", ErrorText.Describe(ex)));
        }

        try
        {
            BitmapFrame frame;
            using (stream)
            {
                // 先只讀標頭：延遲建立且不快取時，讀取寬高不會解碼像素，過大的圖片在配置像素記憶體之前就被拒絕。
                var probe = BitmapDecoder.Create(
                    stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                if (probe is not (PngBitmapDecoder or JpegBitmapDecoder or BmpBitmapDecoder))
                {
                    return Failure(Strings.Get("ImageLoad_UnsupportedFormat"));
                }

                var width = probe.Frames[0].PixelWidth;
                var height = probe.Frames[0].PixelHeight;
                if (width <= 0 || height <= 0 || (long)width * height > MaxPixels)
                {
                    return Failure(Strings.Format("ImageLoad_SizeOutOfRange", width, height, MaxPixels));
                }

                // 整張解碼到記憶體後才關閉檔案；保留檔案本身的像素格式，之後只做一次格式轉換。
                stream.Position = 0;
                frame = BitmapDecoder.Create(
                    stream, BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad)
                    .Frames[0];
            }

            return new ImageLoadResult(FlattenOntoWhite(frame), null);
        }
        catch (Exception ex) when (ex is FileFormatException or NotSupportedException or IOException)
        {
            // WIC 對無法辨識的內容（包括空檔案與文字檔）拋出 NotSupportedException，對損壞的內容拋出 FileFormatException，
            // 讀不到預期的資料（例如檔案被截斷）時拋出 IOException；檔案此時已經成功開啟，因此一律視為內容損壞。
            return Failure(Strings.Get("ImageLoad_Corrupt"));
        }
        catch (OutOfMemoryException)
        {
            // 也涵蓋衍生的 InsufficientMemoryException。
            return Failure(Strings.Get("ImageLoad_OutOfMemory"));
        }
    }

    /// <summary>
    /// 以預乘 Alpha 合成到白色背景：每個色彩通道為「預乘值 + (255 − Alpha)」，完全不透明的像素維持檔案中的值，
    /// 完全透明的像素成為白色。灰階、索引色、16 位元等格式都先由 WIC 轉成 Pbgra32；
    /// Pbgra32 與 Bgr32 每個像素的位元組排列相同，合成直接在同一個緩衝區完成。
    /// </summary>
    private static BitmapSource FlattenOntoWhite(BitmapSource source)
    {
        var premultiplied = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        var width = premultiplied.PixelWidth;
        var height = premultiplied.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        premultiplied.CopyPixels(pixels, stride, 0);
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var background = 255 - pixels[i + 3];
            pixels[i] = (byte)Math.Min(pixels[i] + background, 255);
            pixels[i + 1] = (byte)Math.Min(pixels[i + 1] + background, 255);
            pixels[i + 2] = (byte)Math.Min(pixels[i + 2] + background, 255);
            pixels[i + 3] = 255;
        }

        // 以像素尺寸為準：不沿用圖檔記錄的 DPI。
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
        image.Freeze();
        return image;
    }

    private static ImageLoadResult Failure(string message) => new(null, message);
}
