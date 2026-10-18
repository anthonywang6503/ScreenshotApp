using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenshotApp.Export;

/// <summary>
/// 把圖片放入 Windows 剪貼簿。剪貼簿被其他程式占用時，.NET 會先自行短暫重試（約一秒）；
/// 仍失敗時回傳錯誤碼讓呼叫端提示使用者，不假裝成功。
/// </summary>
internal static class ClipboardService
{
    // 96 DPI 換算成 DIB 使用的每公尺像素數。
    private const int PixelsPerMeterAt96Dpi = 3780;

    public static bool TrySetImage(BitmapSource image, out int errorCode)
    {
        // 以 DIB 位元組串流提供圖片，不用 Clipboard.SetImage：後者每次都會在本處理程序留下一個 GDI bitmap 物件。
        // 需要 CF_BITMAP 的程式由 Windows 從 DIB 自動轉換。
        var data = new DataObject();
        data.SetData(DataFormats.Dib, CreateDib(image));
        try
        {
            Clipboard.SetDataObject(data, copy: true);
            errorCode = 0;
            return true;
        }
        catch (ExternalException ex)
        {
            errorCode = ex.HResult;
            return false;
        }
    }

    // 24 位元、由下而上排列的 DIB（BITMAPINFOHEADER + 像素）。不用 32 位元，以免接收端把未定義的 alpha 位元組當成透明度。
    internal static MemoryStream CreateDib(BitmapSource image)
    {
        var source = new FormatConvertedBitmap(image, PixelFormats.Bgr24, null, 0);
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var stride = (width * 3 + 3) & ~3;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        var stream = new MemoryStream(40 + pixels.Length);
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(40);
            writer.Write(width);
            writer.Write(height);
            writer.Write((short)1);
            writer.Write((short)24);
            writer.Write(0);
            writer.Write(pixels.Length);
            writer.Write(PixelsPerMeterAt96Dpi);
            writer.Write(PixelsPerMeterAt96Dpi);
            writer.Write(0);
            writer.Write(0);
            for (var row = height - 1; row >= 0; row--)
            {
                writer.Write(pixels, row * stride, stride);
            }
        }

        stream.Position = 0;
        return stream;
    }
}
