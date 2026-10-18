using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Resources;

namespace ScreenshotApp.Export;

/// <summary>放入剪貼簿的兩種格式，皆由同一張合成結果產生：24 位元 DIB 與 PNG。</summary>
internal sealed record ClipboardPayload(byte[] Dib, byte[] Png)
{
    /// <summary>剪貼簿上 PNG 格式的名稱（Office、瀏覽器等程式共同使用的已註冊格式）。</summary>
    public const string PngFormat = "PNG";

    /// <summary>由凍結的圖片產生兩種格式；耗時的是 PNG 編碼，不需要 Dispatcher，可在背景執行緒呼叫。</summary>
    public static ClipboardPayload Create(BitmapSource image) =>
        new(ClipboardService.CreateDib(image).ToArray(), PngFileService.Encode(image));

    public DataObject ToDataObject()
    {
        var data = new DataObject();
        data.SetData(DataFormats.Dib, new MemoryStream(Dib));
        data.SetData(PngFormat, new MemoryStream(Png));
        return data;
    }
}

/// <summary>
/// 把圖片放入 Windows 剪貼簿。剪貼簿被其他程式占用時，.NET 會先自行短暫重試（開啟與寫入各最多約一秒），
/// 不另加重試；仍失敗時回傳錯誤碼讓呼叫端提示使用者，不假裝成功。
/// </summary>
internal static class ClipboardService
{
    /// <summary>剪貼簿正被其他程式開啟。</summary>
    public const int CLIPBRD_E_CANT_OPEN = unchecked((int)0x800401D0);

    // 96 DPI 換算成 DIB 使用的每公尺像素數。
    private const int PixelsPerMeterAt96Dpi = 3780;

    /// <summary>放入剪貼簿，內容在程式結束後仍保留。需在 UI（STA）執行緒呼叫。</summary>
    public static bool TrySetImage(ClipboardPayload payload, out int errorCode) =>
        TrySetImage(payload, data => Clipboard.SetDataObject(data, copy: true), out errorCode);

    // 以 DIB 與 PNG 位元組串流提供圖片，不用 Clipboard.SetImage：後者每次都會在本處理程序留下一個 GDI bitmap 物件。
    // 需要 CF_BITMAP 的程式由 Windows 從 DIB 自動轉換。
    internal static bool TrySetImage(ClipboardPayload payload, Action<DataObject> setDataObject, out int errorCode)
    {
        try
        {
            setDataObject(payload.ToDataObject());
            errorCode = 0;
            return true;
        }
        catch (ExternalException ex)
        {
            errorCode = ex.HResult;
            return false;
        }
    }

    /// <summary>複製失敗時給使用者的說明。</summary>
    public static string DescribeError(int errorCode) =>
        errorCode == CLIPBRD_E_CANT_OPEN ? Strings.Get("Editor_CopyFailedBusy") : Strings.Format("Editor_CopyFailed", errorCode);

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
