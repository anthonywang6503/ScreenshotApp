using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using ScreenshotApp.Platform;
using ScreenshotApp.Resources;

namespace ScreenshotApp.Capture;

/// <summary>單色游標的 AND／XOR 平面：每列以 16 位元對齊，位元由高到低對應由左到右的像素。</summary>
internal sealed record CursorShape(int Size, int Hotspot, byte[] AndPlane, byte[] XorPlane);

/// <summary>
/// 選取覆蓋層的十字游標：黑色十字外圍一圈白邊，在任何底色上都看得見。系統十字游標全部以反相像素繪製，
/// 覆蓋層把白色畫面暗化成 50% 灰之後，反相結果與底色只差一個色階而看不見。
/// </summary>
internal static class CrosshairCursor
{
    // 每種尺寸只建立一次並沿用到程式結束：游標數量不隨截圖次數增加，也不會銷毀仍在顯示中的游標。只在 UI 執行緒使用。
    private static readonly Dictionary<int, Cursor> CursorsBySize = [];

    /// <summary>指定 DPI 縮放下的十字游標，尺寸與系統游標在該 DPI 的標準尺寸相同。</summary>
    public static Cursor For(double dpiScale)
    {
        var size = NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CXCURSOR, (uint)Math.Round(dpiScale * 96));
        if (!CursorsBySize.TryGetValue(size, out var cursor))
        {
            cursor = CursorInteropHelper.Create(Create(size));
            CursorsBySize.Add(size, cursor);
        }

        return cursor;
    }

    public static SafeCursorHandle Create(int size)
    {
        var shape = Draw(size);
        var cursor = NativeMethods.CreateCursor(0, shape.Hotspot, shape.Hotspot, size, size, shape.AndPlane, shape.XorPlane);
        if (cursor.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            cursor.Dispose();
            throw new CaptureException(Strings.Format("Capture_ApiFailed", error));
        }

        return cursor;
    }

    /// <summary>
    /// 畫出 size × size 的十字：黑色線寬為奇數，熱點是兩條線交會處的中心像素；黑色像素外圍一圈 1 像素的白邊。
    /// 比例與系統十字游標相同（48 像素時線寬 3、中心到末端 14 像素）。
    /// </summary>
    public static CursorShape Draw(int size)
    {
        var center = size / 2 - 1;
        var halfWidth = (size + 16) / 32 - 1;
        var armLength = size * 3 / 10;
        var stride = (size + 15) / 16 * 2;
        var and = new byte[stride * size];
        var xor = new byte[stride * size];

        // AND 1、XOR 0 是透明；AND 0、XOR 0 是黑；AND 0、XOR 1 是白。不使用會反相底色的 AND 1、XOR 1。
        Array.Fill(and, (byte)0xFF);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = Math.Abs(x - center);
                var dy = Math.Abs(y - center);
                if (!InCross(dx, dy, grow: 1))
                {
                    continue;
                }

                var index = y * stride + x / 8;
                var bit = (byte)(0x80 >> (x % 8));
                and[index] &= (byte)~bit;
                if (!InCross(dx, dy, grow: 0))
                {
                    xor[index] |= bit;
                }
            }
        }

        return new CursorShape(size, center, and, xor);

        bool InCross(int dx, int dy, int grow) =>
            (dx <= armLength + grow && dy <= halfWidth + grow) || (dx <= halfWidth + grow && dy <= armLength + grow);
    }
}
