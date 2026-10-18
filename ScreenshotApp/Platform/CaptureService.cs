using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Resources;

namespace ScreenshotApp.Platform;

/// <summary>截圖失敗。訊息為可直接顯示給使用者的說明。</summary>
internal sealed class CaptureException(string message) : Exception(message);

/// <summary>
/// 以 GDI BitBlt 擷取螢幕畫面。所有 GDI 物件都在方法返回前釋放，結果只以凍結的 bitmap 存在。
/// 像素格式為 Bgr32：BitBlt 結果的 alpha 位元組未定義（通常為 0），以 Bgra32 解讀會變成整張透明的圖片。
/// </summary>
internal static class CaptureService
{
    /// <summary>擷取桌面上的指定範圍（桌面實體像素），影像像素與螢幕像素一一對應。需在 UI 執行緒呼叫。</summary>
    public static BitmapSource Capture(PixelRect desktopRect)
    {
        var width = desktopRect.Width;
        var height = desktopRect.Height;

        using var screen = NativeMethods.GetDC(0);
        ThrowIf(screen.IsInvalid);
        using var memory = NativeMethods.CreateCompatibleDC(screen);
        ThrowIf(memory.IsInvalid);
        using var bitmap = NativeMethods.CreateCompatibleBitmap(screen, width, height);
        ThrowIf(bitmap.IsInvalid);

        var previous = NativeMethods.SelectObject(memory, bitmap);
        ThrowIf(previous == 0);
        try
        {
            // CAPTUREBLT 讓分層視窗（例如半透明的選單與工具提示）一併被擷取。
            ThrowIf(!NativeMethods.BitBlt(
                memory, 0, 0, width, height, screen, desktopRect.X, desktopRect.Y, NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT));
        }
        finally
        {
            // 讀取像素時 bitmap 不得仍選入 DC，刪除記憶體 DC 前也必須換回原本的物件。
            NativeMethods.SelectObject(memory, previous);
        }

        var image = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
        image.Lock();
        try
        {
            var header = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                biWidth = width,
                // 負的高度代表由上而下的列順序，與 WPF 的像素排列相同。
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeMethods.BI_RGB,
            };
            ThrowIf(NativeMethods.GetDIBits(
                screen, bitmap, 0, (uint)height, image.BackBuffer, ref header, NativeMethods.DIB_RGB_COLORS) != height);
            image.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            image.Unlock();
        }

        image.Freeze();
        return image;
    }

    private static void ThrowIf(bool failed)
    {
        if (failed)
        {
            throw new CaptureException(Strings.Format("Capture_ApiFailed", Marshal.GetLastPInvokeError()));
        }
    }
}
