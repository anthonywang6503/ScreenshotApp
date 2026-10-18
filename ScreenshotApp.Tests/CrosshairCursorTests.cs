using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Capture;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

/// <summary>
/// 選取覆蓋層的十字游標在任何底色上都看得見。系統十字游標全部以反相像素繪製，
/// 覆蓋層把白色畫面暗化成 50% 灰之後，反相結果與底色只差一個色階而看不見。
/// </summary>
[Collection(GdiObjectCountCollection.Name)]
public partial class CrosshairCursorTests
{
    // 系統游標在各 DPI 下的標準尺寸（32、48、64、96、128），加上一個寬度不是 16 倍數的尺寸以驗證每列的補齊。
    public static TheoryData<int> Sizes => new() { 32, 40, 48, 64, 96, 128 };

    // 純黑、暗化後的白色畫面（50% 灰的兩側）與純白。
    private static readonly byte[] Backgrounds = [0, 127, 128, 255];

    private const int DI_NORMAL = 0x0003;

    private enum Ink
    {
        Transparent,
        Black,
        White,
        Invert,
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Draw_UsesNoInvertedPixels(int size)
    {
        var shape = CrosshairCursor.Draw(size);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                Assert.NotEqual(Ink.Invert, PixelAt(shape, x, y));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Draw_EveryPartOfBothLinesIsVisibleOnAnyBackground(int size)
    {
        var shape = CrosshairCursor.Draw(size);
        var center = shape.Hotspot;

        foreach (var background in Backgrounds)
        {
            // 黑色線的每個位置，垂直於線的整段墨水（黑芯加白邊）中至少有一個像素與底色相差半個灰階範圍以上。
            for (var x = 0; x < size; x++)
            {
                if (PixelAt(shape, x, center) == Ink.Black)
                {
                    Assert.True(MaxContrast(shape, x, center, 0, 1, background) >= 127, $"底色 {background}：橫線 x={x} 看不見");
                }
            }

            for (var y = 0; y < size; y++)
            {
                if (PixelAt(shape, center, y) == Ink.Black)
                {
                    Assert.True(MaxContrast(shape, center, y, 1, 0, background) >= 127, $"底色 {background}：直線 y={y} 看不見");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Draw_CrossSpansAtLeastHalfOfTheCursor(int size)
    {
        var shape = CrosshairCursor.Draw(size);

        var horizontal = Run(shape, shape.Hotspot, shape.Hotspot, -1, 0, Ink.Black) + 1 + Run(shape, shape.Hotspot, shape.Hotspot, 1, 0, Ink.Black);
        var vertical = Run(shape, shape.Hotspot, shape.Hotspot, 0, -1, Ink.Black) + 1 + Run(shape, shape.Hotspot, shape.Hotspot, 0, 1, Ink.Black);

        Assert.True(horizontal >= size / 2, $"橫線長度 {horizontal}");
        Assert.True(vertical >= size / 2, $"直線長度 {vertical}");
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Draw_HotspotIsTheCenterPixelOfBothLines(int size)
    {
        var shape = CrosshairCursor.Draw(size);
        var c = shape.Hotspot;

        Assert.Equal(Ink.Black, PixelAt(shape, c, c));

        // 兩條線從熱點往兩側延伸的長度相同。
        var left = Run(shape, c, c, -1, 0, Ink.Black);
        Assert.Equal(left, Run(shape, c, c, 1, 0, Ink.Black));
        var up = Run(shape, c, c, 0, -1, Ink.Black);
        Assert.Equal(up, Run(shape, c, c, 0, 1, Ink.Black));

        // 在線的末端量線寬：熱點所在的列（行）兩側的黑色像素數相同，線寬為奇數且以熱點為中心。
        var tipX = c - left;
        Assert.Equal(Run(shape, tipX, c, 0, -1, Ink.Black), Run(shape, tipX, c, 0, 1, Ink.Black));
        var tipY = c - up;
        Assert.Equal(Run(shape, c, tipY, -1, 0, Ink.Black), Run(shape, c, tipY, 1, 0, Ink.Black));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Draw_EveryBlackPixelIsOutlinedInsideTheCursor(int size)
    {
        var shape = CrosshairCursor.Draw(size);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (PixelAt(shape, x, y) != Ink.Black)
                {
                    continue;
                }

                // 黑色像素的八個鄰居都在游標範圍內且不透明：在黑色底上，外圍的白邊讓十字仍然看得見。
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        Assert.InRange(x + dx, 0, size - 1);
                        Assert.InRange(y + dy, 0, size - 1);
                        Assert.NotEqual(Ink.Transparent, PixelAt(shape, x + dx, y + dy));
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Draw_RowsArePaddedToWordsAndThePaddingIsTransparent(int size)
    {
        var shape = CrosshairCursor.Draw(size);
        var stride = (size + 15) / 16 * 2;

        Assert.Equal(size, shape.Size);
        Assert.Equal(stride * size, shape.AndPlane.Length);
        Assert.Equal(stride * size, shape.XorPlane.Length);
        for (var y = 0; y < size; y++)
        {
            for (var x = size; x < stride * 8; x++)
            {
                Assert.Equal(Ink.Transparent, PixelAt(shape, x, y));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Create_WindowsReportsAMonochromeCursorWithTheHotspotAtTheCenter(int size)
    {
        using var cursor = CrosshairCursor.Create(size);
        var hotspot = CrosshairCursor.Draw(size).Hotspot;

        Assert.True(GetIconInfo(cursor, out var info));
        try
        {
            Assert.Equal(0, info.fIcon);
            Assert.Equal(hotspot, info.xHotspot);
            Assert.Equal(hotspot, info.yHotspot);
            Assert.Equal(0, info.hbmColor);
            Assert.NotEqual(0, GetObject(info.hbmMask, Marshal.SizeOf<BITMAP>(), out var mask));
            Assert.Equal(size, mask.bmWidth);
            Assert.Equal(size * 2, mask.bmHeight);
        }
        finally
        {
            NativeMethods.DeleteObject(info.hbmMask);
            if (info.hbmColor != 0)
            {
                NativeMethods.DeleteObject(info.hbmColor);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Create_DrawnByWindows_MatchesTheShapeAndIsVisibleOnAnyBackground(int size)
    {
        using var cursor = CrosshairCursor.Create(size);
        var shape = CrosshairCursor.Draw(size);

        foreach (var background in Backgrounds)
        {
            var drawn = DrawOnBackground(cursor, size, background);
            var maxContrast = 0;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    Assert.Equal(Composite(PixelAt(shape, x, y), background), drawn[x, y]);
                    maxContrast = Math.Max(maxContrast, Math.Abs(drawn[x, y] - background));
                }
            }

            Assert.Equal(0, drawn[shape.Hotspot, shape.Hotspot]);
            Assert.True(maxContrast >= 127, $"底色 {background} 的最大對比 {maxContrast}");
        }
    }

    [Fact]
    public void Create_Dispose_DestroysTheCursor()
    {
        var userObjects = NativeTestHelpers.UserObjectCount();
        var gdiObjects = NativeTestHelpers.GdiObjectCount();

        var cursor = CrosshairCursor.Create(48);
        Assert.Equal(userObjects + 1, NativeTestHelpers.UserObjectCount());

        cursor.Dispose();
        Assert.Equal(userObjects, NativeTestHelpers.UserObjectCount());
        Assert.Equal(gdiObjects, NativeTestHelpers.GdiObjectCount());
    }

    [Fact]
    public void For_ReusesOneCursorPerSize()
    {
        Assert.Same(CrosshairCursor.For(1.75), CrosshairCursor.For(1.75));
        Assert.NotSame(CrosshairCursor.For(1.0), CrosshairCursor.For(2.0));

        var userObjects = NativeTestHelpers.UserObjectCount();
        for (var i = 0; i < 10; i++)
        {
            CrosshairCursor.For(1.75);
        }

        Assert.Equal(userObjects, NativeTestHelpers.UserObjectCount());
    }

    [Fact]
    public void SelectionOverlay_UsesTheCrosshairForItsDpi()
    {
        StaRunner.Run(() =>
        {
            NativeTestHelpers.UsePerMonitorV2();
            var image = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgr32, null, new byte[64 * 64 * 4], 64 * 4);
            using var overlay = new SelectionOverlay(new MonitorSnapshot(new MonitorInfo(0, new PixelRect(0, 0, 64, 64)), image));

            Assert.Same(CrosshairCursor.For(VisualTreeHelper.GetDpi(overlay).DpiScaleX), overlay.Cursor);
        });
    }

    private static Ink PixelAt(CursorShape shape, int x, int y)
    {
        var stride = shape.AndPlane.Length / shape.Size;
        var index = y * stride + x / 8;
        var bit = 0x80 >> (x % 8);
        return ((shape.AndPlane[index] & bit) != 0, (shape.XorPlane[index] & bit) != 0) switch
        {
            (true, false) => Ink.Transparent,
            (false, false) => Ink.Black,
            (false, true) => Ink.White,
            _ => Ink.Invert,
        };
    }

    /// <summary>單色游標的顯示規則：先以 AND 遮罩保留或清除底色，再以 XOR 遮罩反相。</summary>
    private static int Composite(Ink ink, int background) => ink switch
    {
        Ink.Transparent => background,
        Ink.Black => 0,
        Ink.White => 255,
        _ => 255 - background,
    };

    /// <summary>從 (x, y) 沿 (dx, dy) 方向連續為指定墨水的像素數，不含起點。</summary>
    private static int Run(CursorShape shape, int x, int y, int dx, int dy, Ink ink)
    {
        var count = 0;
        for (x += dx, y += dy; x >= 0 && y >= 0 && x < shape.Size && y < shape.Size && PixelAt(shape, x, y) == ink; x += dx, y += dy)
        {
            count++;
        }

        return count;
    }

    /// <summary>經過 (x, y)、沿 (dx, dy) 方向連續不透明的一段像素中，與底色相差最大的值。</summary>
    private static int MaxContrast(CursorShape shape, int x, int y, int dx, int dy, int background)
    {
        var max = Math.Abs(Composite(PixelAt(shape, x, y), background) - background);
        foreach (var sign in new[] { -1, 1 })
        {
            for (int px = x + sign * dx, py = y + sign * dy;
                 px >= 0 && py >= 0 && px < shape.Size && py < shape.Size && PixelAt(shape, px, py) != Ink.Transparent;
                 px += sign * dx, py += sign * dy)
            {
                max = Math.Max(max, Math.Abs(Composite(PixelAt(shape, px, py), background) - background));
            }
        }

        return max;
    }

    /// <summary>以 Windows 繪製游標的函式把游標畫在指定灰階的底色上，回傳每個像素的灰階值（[x, y]）。</summary>
    private static byte[,] DrawOnBackground(SafeCursorHandle cursor, int size, byte background)
    {
        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = size,
            biHeight = -size,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = NativeMethods.BI_RGB,
        };
        using var screen = NativeMethods.GetDC(0);
        using var memory = NativeMethods.CreateCompatibleDC(screen);
        var bitmap = CreateDIBSection(screen, ref header, NativeMethods.DIB_RGB_COLORS, out var bits, 0, 0);
        Assert.NotEqual(0, bitmap);
        var previous = NativeMethods.SelectObject(memory, bitmap);
        try
        {
            var pixels = new byte[size * size * 4];
            Array.Fill(pixels, background);
            Marshal.Copy(pixels, 0, bits, pixels.Length);

            Assert.True(DrawIconEx(memory, 0, 0, cursor, 0, 0, 0, 0, DI_NORMAL));
            GdiFlush();
            Marshal.Copy(bits, pixels, 0, pixels.Length);

            var result = new byte[size, size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    result[x, y] = pixels[(y * size + x) * 4];
                }
            }

            return result;
        }
        finally
        {
            NativeMethods.SelectObject(memory, previous);
            NativeMethods.DeleteObject(bitmap);
        }
    }

    // 未停用執行階段封送時，LibraryImport 只能直接傳遞本組件宣告的結構，因此不沿用應用程式的同名結構。
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public int fIcon;
        public int xHotspot;
        public int yHotspot;
        public nint hbmMask;
        public nint hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public nint bmBits;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetIconInfo(SafeCursorHandle hIcon, out ICONINFO piconinfo);

    [LibraryImport("gdi32.dll", EntryPoint = "GetObjectW")]
    private static partial int GetObject(nint h, int c, out BITMAP pv);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateDIBSection(
        SafeScreenDcHandle hdc, ref BITMAPINFOHEADER pbmi, uint usage, out nint ppvBits, nint hSection, uint offset);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DrawIconEx(
        SafeMemoryDcHandle hdc, int xLeft, int yTop, SafeCursorHandle hIcon, int cxWidth, int cyWidth, int istepIfAniCur, nint hbrFlickerFreeDraw, int diFlags);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GdiFlush();
}
