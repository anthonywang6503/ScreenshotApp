using System.Runtime.InteropServices;

namespace ScreenshotApp.Platform;

/// <summary>Win32 API 宣告。所有平台呼叫集中於此，只宣告實際使用的 API。</summary>
internal static partial class NativeMethods
{
    public const int WM_DISPLAYCHANGE = 0x007E;
    public const int WM_HOTKEY = 0x0312;
    public const int WM_DPICHANGED = 0x02E0;
    public const uint MOD_NOREPEAT = 0x4000;
    public const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;
    public const int ASFW_ANY = -1;

    public const int WS_POPUP = unchecked((int)0x80000000);
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int SW_SHOWNA = 8;

    public const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

    public const int SM_CXCURSOR = 13;

    public const uint SRCCOPY = 0x00CC0020;
    public const uint CAPTUREBLT = 0x40000000;
    public const uint BI_RGB = 0;
    public const uint DIB_RGB_COLORS = 0;

    public static readonly nint HWND_MESSAGE = -3;
    public static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
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

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport("user32.dll")]
    public static partial nint GetThreadDpiAwarenessContext();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AreDpiAwarenessContextsEqual(nint dpiContextA, nint dpiContextB);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AllowSetForegroundWindow(int dwProcessId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool EnumDisplayMonitors(
        nint hdc, nint lprcClip, delegate* unmanaged<nint, nint, RECT*, nint, int> lpfnEnum, nint dwData);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowEnabled(nint hWnd);

    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetricsForDpi(int nIndex, uint dpi);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial SafeCursorHandle CreateCursor(
        nint hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, byte[] pvANDPlane, byte[] pvXORPlane);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyCursor(nint hCursor);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmFlush();

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial SafeScreenDcHandle GetDC(nint hWnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hWnd, nint hDC);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial SafeMemoryDcHandle CreateCompatibleDC(SafeScreenDcHandle hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial SafeGdiObjectHandle CreateCompatibleBitmap(SafeScreenDcHandle hdc, int cx, int cy);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint ho);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial nint SelectObject(SafeMemoryDcHandle hdc, SafeGdiObjectHandle h);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(SafeMemoryDcHandle hdc, nint h);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(
        SafeMemoryDcHandle hdc, int x, int y, int cx, int cy, SafeScreenDcHandle hdcSrc, int x1, int y1, uint rop);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial int GetDIBits(
        SafeScreenDcHandle hdc, SafeGdiObjectHandle hbm, uint start, uint cLines, nint lpvBits, ref BITMAPINFOHEADER lpbmi, uint usage);
}
