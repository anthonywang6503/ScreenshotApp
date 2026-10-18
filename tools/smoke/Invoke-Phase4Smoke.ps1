#Requires -Version 7
<#
.SYNOPSIS
    ScreenshotApp 圖片輸入輸出煙霧驗證：剪貼簿的 PNG 與點陣圖兩種格式、另存 PNG 與剪貼簿逐位元組相同、記住儲存位置、
    未匯出變更的詢問（新截圖、關閉編輯器、開啟圖片、系統匣結束）、開啟 BMP／JPEG 與損壞檔、另存失敗、剪貼簿被占用；
    最後以連續完整循環量測資源是否成長，並量測截圖、開啟圖片、複製與另存的延遲。

.DESCRIPTION
    會在目前的桌面工作階段實際啟動 ScreenshotApp 並操作；執行期間（預設 30 次循環約 8～10 分鐘）請勿操作滑鼠與鍵盤。
    截圖對象是腳本顯示的已知圖樣視窗（Show-TestPattern.ps1）；開啟圖片用的 BMP、JPEG 與損壞檔由腳本產生在證據資料夾。

    保護使用者的措施：
    - 開始前等使用者閒置；每次操作前檢查自上次模擬輸入後是否有使用者自己的鍵盤或滑鼠操作，有就立即停止，不再送出任何輸入。
    - 按鈕以 UI Automation 按下、檔案對話框以視窗訊息填入路徑，盡量不送出模擬按鍵；必須送出的按鍵與滑鼠操作只在 ScreenshotApp 的視窗位於前景時送出。
    - 快捷鍵只在確認由 ScreenshotApp 註冊時送出（按鍵由系統攔截，不會落到其他應用程式）。
    - 證據截圖以 PrintWindow 只擷取 ScreenshotApp 的視窗。
    - 結束時還原 settings.json 與故障日誌；剪貼簿只在驗證期間沒有被其他程式改動時才放回原本的內容。
    - 驗證剪貼簿被占用時，腳本自己開啟剪貼簿約數秒，期間其他程式也無法使用剪貼簿。

.PARAMETER ExePath
    要驗證的 ScreenshotApp.exe；預設為 Release 建置輸出。

.PARAMETER EvidenceDirectory
    存放截圖、測試檔與結果清單的資料夾。

.PARAMETER Cycles
    連續完整循環的次數：每次取消一次截圖、截圖開啟編輯器、畫矩形、複製並驗證、另存 PNG、關閉編輯器，之後量測 GDI 與 USER 物件、
    Handle 與記憶體。0 表示不執行循環。

.PARAMETER IdleSeconds
    開始前需要的鍵盤與滑鼠閒置秒數；有人正在使用電腦時先等待，避免模擬輸入干擾使用者。

.PARAMETER MaxIdleWaitMinutes
    等待閒置的上限；超過時不做任何變更即結束。
#>
param(
    [string]$ExePath = (Join-Path $PSScriptRoot '..\..\ScreenshotApp\bin\Release\net10.0-windows\ScreenshotApp.exe'),
    [string]$EvidenceDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'ScreenshotApp.Smoke4'),
    [int]$Cycles = 30,
    [int]$IdleSeconds = 60,
    [int]$MaxIdleWaitMinutes = 30
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Set-StrictMode -Version Latest

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type -Language CSharp -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Phase4Native
{
    public const int WM_CLOSE = 0x0010;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_TRAYMOUSEMESSAGE = 0x0400 + 1024;
    public const ushort VK_RETURN = 0x0D;
    public const ushort VK_SHIFT = 0x10;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_MENU = 0x12;
    public const ushort VK_ESCAPE = 0x1B;
    public const ushort VK_LEFT = 0x25;
    public const ushort VK_RIGHT = 0x27;
    public const ushort VK_DELETE = 0x2E;
    public const ushort VK_0 = 0x30;
    public const ushort VK_2 = 0x32;
    public const int SW_HIDE = 0;
    public const int SW_SHOWNOACTIVATE = 4;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct NOTIFYICONIDENTIFIER { public uint cbSize; public IntPtr hWnd; public uint uID; public Guid guidItem; }

    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [StructLayout(LayoutKind.Sequential)]
    struct CURSORINFO { public uint cbSize; public uint flags; public IntPtr hCursor; public POINT ptScreenPos; }

    [StructLayout(LayoutKind.Sequential)]
    struct ICONINFO { public int fIcon; public int xHotspot; public int yHotspot; public IntPtr hbmMask; public IntPtr hbmColor; }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER { public uint biSize; public int biWidth; public int biHeight; public ushort biPlanes; public ushort biBitCount; public uint biCompression; public uint biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter; public uint biClrUsed; public uint biClrImportant; }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public INPUTUNION u; }

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr process, uint flags);
    [DllImport("shell32.dll")] static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT rect);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
    [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CURSORINFO info);
    [DllImport("user32.dll")] static extern bool GetIconInfo(IntPtr icon, out ICONINFO info);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr hdc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER info, uint usage);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")] public static extern int GetSystemMetricsForDpi(int index, uint dpi);

    static int lastInjected = Environment.TickCount;

    static uint LastInputTick()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
        GetLastInputInfo(ref info);
        return info.dwTime;
    }

    public static uint IdleMilliseconds() { return unchecked((uint)Environment.TickCount - LastInputTick()); }

    // 自本腳本最後一次模擬輸入之後，是否還有晚於 marginMilliseconds 的輸入；本腳本沒有在送輸入時，那就是使用者自己的操作。
    public static bool UserInputSinceInjection(int marginMilliseconds)
    {
        return unchecked((int)(LastInputTick() - (uint)lastInjected)) > marginMilliseconds;
    }

    public static void MoveCursor(int x, int y)
    {
        SetCursorPos(x, y);
        lastInjected = Environment.TickCount;
    }

    public static IntPtr RootWindowAt(int x, int y) { return GetAncestor(WindowFromPoint(new POINT { X = x, Y = y }), 2); }

    // 目前顯示中的單色游標：{寬, 高, 熱點 X, 熱點 Y, 黑, 白, 反相} 像素數；彩色游標或取不到時回傳 null。
    public static int[] CurrentCursor()
    {
        var info = new CURSORINFO { cbSize = (uint)Marshal.SizeOf(typeof(CURSORINFO)) };
        ICONINFO icon;
        if (!GetCursorInfo(ref info) || info.hCursor == IntPtr.Zero || !GetIconInfo(info.hCursor, out icon)) return null;
        var dc = GetDC(IntPtr.Zero);
        try
        {
            if (icon.hbmColor != IntPtr.Zero) return null;
            // 先只取得遮罩尺寸，再以由上而下的 32 位元格式讀出：單色游標的上半是 AND 遮罩、下半是 XOR 遮罩。
            var header = new BITMAPINFOHEADER { biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)) };
            if (GetDIBits(dc, icon.hbmMask, 0, 0, null, ref header, 0) == 0) return null;
            int width = header.biWidth, height = Math.Abs(header.biHeight), half = height / 2;
            header = new BITMAPINFOHEADER { biSize = header.biSize, biWidth = width, biHeight = -height, biPlanes = 1, biBitCount = 32 };
            var bits = new byte[width * height * 4];
            if (GetDIBits(dc, icon.hbmMask, 0, (uint)height, bits, ref header, 0) == 0) return null;
            int black = 0, white = 0, invert = 0;
            for (int y = 0; y < half; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool and = bits[(y * width + x) * 4] != 0, xor = bits[((y + half) * width + x) * 4] != 0;
                    if (!and && !xor) black++; else if (!and) white++; else if (xor) invert++;
                }
            }
            return new[] { width, half, icon.xHotspot, icon.yHotspot, black, white, invert };
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, dc);
            DeleteObject(icon.hbmMask);
            if (icon.hbmColor != IntPtr.Zero) DeleteObject(icon.hbmColor);
        }
    }

    static INPUT KeyInput(ushort vk, ushort scan, uint flags)
    {
        var input = new INPUT { type = 1 };
        input.u.ki.wVk = vk;
        input.u.ki.wScan = scan;
        input.u.ki.dwFlags = flags;
        return input;
    }

    static INPUT MouseInput(uint flags, uint data)
    {
        var input = new INPUT { type = 0 };
        input.u.mi.dwFlags = flags;
        input.u.mi.mouseData = data;
        return input;
    }

    static uint Send(params INPUT[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        lastInjected = Environment.TickCount;
        return sent;
    }

    // 依序按下、反序放開，例如 Ctrl+Shift+2。
    public static uint PressKeys(params ushort[] keys)
    {
        var inputs = new List<INPUT>();
        foreach (var key in keys) inputs.Add(KeyInput(key, 0, 0));
        for (var i = keys.Length - 1; i >= 0; i--) inputs.Add(KeyInput(keys[i], 0, 2));
        return Send(inputs.ToArray());
    }

    // 以 Unicode 字元直接輸入（不經過輸入法），每個 UTF-16 單元一次按下與放開。
    public static uint TypeUnicode(string text)
    {
        var inputs = new List<INPUT>();
        foreach (var c in text)
        {
            inputs.Add(KeyInput(0, c, 0x0004));
            inputs.Add(KeyInput(0, c, 0x0004 | 0x0002));
        }
        return Send(inputs.ToArray());
    }

    public static uint LeftDown() { return Send(MouseInput(0x0002, 0)); }
    public static uint LeftUp() { return Send(MouseInput(0x0004, 0)); }
    public static uint LeftClick() { return Send(MouseInput(0x0002, 0), MouseInput(0x0004, 0)); }

    public static List<IntPtr> TopLevelWindows(uint processId)
    {
        var windows = new List<IntPtr>();
        EnumWindows((hWnd, _) =>
        {
            uint owner;
            GetWindowThreadProcessId(hWnd, out owner);
            if (owner == processId) windows.Add(hWnd);
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public static string ClassOf(IntPtr hWnd) { var sb = new StringBuilder(256); GetClassName(hWnd, sb, sb.Capacity); return sb.ToString(); }

    public static string TextOf(IntPtr hWnd) { var sb = new StringBuilder(512); GetWindowText(hWnd, sb, sb.Capacity); return sb.ToString(); }

    public static List<IntPtr> HiddenFormsWindows(uint processId)
    {
        var result = new List<IntPtr>();
        foreach (var hWnd in TopLevelWindows(processId))
            if (!IsWindowVisible(hWnd) && ClassOf(hWnd).StartsWith("WindowsForms10.Window")) result.Add(hWnd);
        return result;
    }

    public static bool TryFindTrayIcon(uint processId, out IntPtr window, out uint id)
    {
        foreach (var hWnd in HiddenFormsWindows(processId))
        {
            for (uint i = 1; i <= 20; i++)
            {
                var identifier = new NOTIFYICONIDENTIFIER { cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONIDENTIFIER)), hWnd = hWnd, uID = i };
                RECT rect;
                if (Shell_NotifyIconGetRect(ref identifier, out rect) == 0) { window = hWnd; id = i; return true; }
            }
        }
        window = IntPtr.Zero;
        id = 0;
        return false;
    }

    public static List<int[]> Monitors()
    {
        var list = new List<int[]>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr hdc, ref RECT r, IntPtr data) =>
        {
            uint dpiX, dpiY;
            var percent = GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) == 0 ? (int)(dpiX * 100 / 96) : 0;
            list.Add(new[] { r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, percent });
            return true;
        }, IntPtr.Zero);
        return list;
    }

    // 與 Show-TestPattern.ps1 相同的編碼，回傳 0xRRGGBB。
    public static int PatternColor(int x, int y) { return ((x & 0xFF) << 16) | ((y & 0xFF) << 8) | ((((x >> 8) & 0xF) << 4) | ((y >> 8) & 0xF)); }

    static int Rgb(byte[] pixels, int stride, int x, int y) { var i = y * stride + x * 4; return (pixels[i + 2] << 16) | (pixels[i + 1] << 8) | pixels[i]; }

    // 影像中 (x, y, width, height) 範圍與圖樣不一致的像素數；(patternX, patternY) 為影像原點在圖樣中的座標。
    public static int CountPatternMismatches(byte[] pixels, int stride, int x, int y, int width, int height, int patternX, int patternY, out string first)
    {
        var count = 0;
        first = "";
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                var actual = Rgb(pixels, stride, column, row);
                var expected = PatternColor(patternX + column, patternY + row);
                if (actual != expected)
                {
                    if (count == 0) first = string.Format("({0},{1}) 實際 #{2:X6} 預期 #{3:X6}", column, row, actual, expected);
                    count++;
                }
            }
        }
        return count;
    }

    public static int CountColor(byte[] pixels, int stride, int x, int y, int width, int height, int rgb)
    {
        var count = 0;
        for (var row = y; row < y + height; row++)
            for (var column = x; column < x + width; column++)
                if (Rgb(pixels, stride, column, row) == rgb) count++;
        return count;
    }

    // 帶紅色調的像素（紅明顯高於綠與藍），用於確認反鋸齒的紅色標註出現在預期範圍。
    public static int CountReddish(byte[] pixels, int stride, int x, int y, int width, int height)
    {
        var count = 0;
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                var i = row * stride + column * 4;
                if (pixels[i + 2] >= 200 && pixels[i + 1] <= 60 && pixels[i] <= 60) count++;
            }
        }
        return count;
    }

    public static int CountDifferent(byte[] a, int strideA, int ax, int ay, byte[] b, int strideB, int bx, int by, int width, int height)
    {
        var count = 0;
        for (var row = 0; row < height; row++)
            for (var column = 0; column < width; column++)
                if (Rgb(a, strideA, ax + column, ay + row) != Rgb(b, strideB, bx + column, by + row)) count++;
        return count;
    }

    // 在影像中尋找與圖樣 (patternX, patternY) 起、width × height 完全一致的區塊；找到時回傳區塊左上角在影像中的位置。
    public static bool FindPatternBlock(byte[] pixels, int stride, int imageWidth, int imageHeight, int patternX, int patternY, int width, int height, out int foundX, out int foundY)
    {
        var anchor = PatternColor(patternX, patternY);
        string ignored;
        for (var y = 0; y + height <= imageHeight; y++)
        {
            for (var x = 0; x + width <= imageWidth; x++)
            {
                if (Rgb(pixels, stride, x, y) != anchor) continue;
                if (CountPatternMismatches(pixels, stride, x, y, width, height, patternX - x, patternY - y, out ignored) == 0)
                {
                    foundX = x;
                    foundY = y;
                    return true;
                }
            }
        }
        foundX = -1;
        foundY = -1;
        return false;
    }

    public const int WM_COMMAND = 0x0111;
    public const int IDOK = 1;
    public const int IDCANCEL = 2;
    public const int IDYES = 6;
    const int WM_SETTEXT = 0x000C;
    const int TDM_CLICK_BUTTON = 0x0400 + 102;
    const uint GW_OWNER = 4;

    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr hWnd);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hWnd, uint command);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] public static extern bool CloseClipboard();

    public static IntPtr OwnerOf(IntPtr hWnd) { return GetWindow(hWnd, GW_OWNER); }

    // 滑鼠右鍵按下並放開：送到游標下的視窗，與鍵盤焦點在哪個視窗無關。
    public static uint RightClick() { return Send(MouseInput(0x0008, 0), MouseInput(0x0010, 0)); }

    public static List<IntPtr> Descendants(IntPtr parent)
    {
        var list = new List<IntPtr>();
        EnumChildWindows(parent, (hWnd, _) => { list.Add(hWnd); return true; }, IntPtr.Zero);
        return list;
    }

    // 檔案對話框的「檔案名稱」編輯框：開檔與存檔對話框中唯一位於可編輯下拉方塊（ComboBox 或 ComboBoxEx32）之內的可見 Edit；
    // 找不到時退而使用第一個可見的 Edit。
    public static IntPtr FileNameEdit(IntPtr dialog)
    {
        var fallback = IntPtr.Zero;
        foreach (var hWnd in Descendants(dialog))
        {
            if (ClassOf(hWnd) != "Edit" || !IsWindowVisible(hWnd)) continue;
            for (var ancestor = GetParent(hWnd); ancestor != IntPtr.Zero && ancestor != dialog; ancestor = GetParent(ancestor))
            {
                var name = ClassOf(ancestor);
                if (name == "ComboBox" || name == "ComboBoxEx32") return hWnd;
            }
            if (fallback == IntPtr.Zero) fallback = hWnd;
        }
        return fallback;
    }

    public static void SetText(IntPtr hWnd, string text) { SendMessage(hWnd, WM_SETTEXT, IntPtr.Zero, text); }

    // 訊息方塊的內文（Static 控制項的文字）。
    public static string StaticText(IntPtr dialog)
    {
        var sb = new StringBuilder();
        foreach (var hWnd in Descendants(dialog))
        {
            if (ClassOf(hWnd) == "Static") sb.Append(TextOf(hWnd)).Append('\n');
        }
        return sb.ToString();
    }

    // TaskDialog 樣式的確認對話框以 TDM_CLICK_BUTTON 按下按鈕；傳統對話框不處理這個訊息，再改送 WM_COMMAND。
    public static void ClickDialogButton(IntPtr dialog, int id)
    {
        SendMessage(dialog, TDM_CLICK_BUTTON, (IntPtr)id, IntPtr.Zero);
        if (IsWindow(dialog)) PostMessage(dialog, WM_COMMAND, (IntPtr)id, IntPtr.Zero);
    }

    // 產生與 Show-TestPattern.ps1 相同編碼的 24 位元圖樣（由上而下、每列補齊 4 位元組），供開啟圖片的測試檔使用。
    public static byte[] PatternBgr24(int width, int height, out int stride)
    {
        stride = (width * 3 + 3) & ~3;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var rgb = PatternColor(x, y);
                var i = y * stride + x * 3;
                pixels[i] = (byte)(rgb & 0xFF);
                pixels[i + 1] = (byte)((rgb >> 8) & 0xFF);
                pixels[i + 2] = (byte)((rgb >> 16) & 0xFF);
            }
        }
        return pixels;
    }
}
'@

# 以 Per-Monitor V2 執行本腳本的座標計算與螢幕擷取，取得實體像素。
[void][Phase4Native]::SetThreadDpiAwarenessContext([IntPtr]-4)

$ModControl = 0x2
$ModShift = 0x4
$ModNoRepeat = 0x4000
$ErrorHotkeyAlreadyRegistered = 1409
$OverlayTitle = 'ScreenshotApp 區域選取'
$EditorTitle = 'ScreenshotApp 編輯器'
$PatternTitle = 'ScreenshotApp 煙霧驗證圖樣'
$Sentinel = 'ScreenshotApp 煙霧驗證：等待剪貼簿更新'
$Red = 0xFF0000
$DataDirectory = Join-Path $env:LOCALAPPDATA 'ScreenshotApp'
$SettingsPath = Join-Path $DataDirectory 'settings.json'
$LogDirectory = Join-Path $DataDirectory 'logs'
$LogPath = Join-Path $LogDirectory 'error.log'
$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

$results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$Check, [bool]$Passed, [string]$Detail) {
    $status = if ($Passed) { 'PASS' } else { 'FAIL' }
    $results.Add([pscustomobject]@{ Status = $status; Check = $Check; Detail = $Detail })
    Write-Host "[$status] $Check — $Detail"
}

function Add-Info([string]$Check, [string]$Detail) {
    $results.Add([pscustomobject]@{ Status = 'INFO'; Check = $Check; Detail = $Detail })
    Write-Host "[INFO] $Check — $Detail"
}

function Wait-Until([scriptblock]$Condition, [int]$TimeoutMilliseconds = 5000) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        $value = & $Condition
        if ($value) { return $value }
        Start-Sleep -Milliseconds 50
    }
    return $null
}

# 使用者在驗證途中開始操作電腦時立即停止，不再送出任何模擬輸入。
function Stop-Simulation([string]$Reason) { throw [System.OperationCanceledException]::new($Reason) }

function Assert-NoUserInput {
    if ([Phase4Native]::UserInputSinceInjection(300)) { Stop-Simulation '偵測到使用者自己的鍵盤或滑鼠操作' }
}

# 滑鼠操作前確認前景與目標位置的視窗都屬於 ScreenshotApp（允許清單內的視窗）。
function Assert-Target([int]$X, [int]$Y, [IntPtr[]]$Allowed) {
    Assert-NoUserInput
    $foreground = [Phase4Native]::GetForegroundWindow()
    if ($Allowed -notcontains $foreground) {
        $editor = @($Allowed | Where-Object { [Phase4Native]::TextOf($_) -eq $EditorTitle }) | Select-Object -First 1
        if (-not $editor -or -not (Restore-EditorForeground $editor)) {
            Stop-Simulation ("前景視窗不是 ScreenshotApp（{0}）" -f [Phase4Native]::ClassOf($foreground))
        }
    }
    $root = [Phase4Native]::RootWindowAt($X, $Y)
    if ($Allowed -notcontains $root) { Stop-Simulation ("({0},{1}) 的視窗不是 ScreenshotApp（{2}）" -f $X, $Y, [Phase4Native]::ClassOf($root)) }
}

# 沒有使用者操作、前景卻被其他視窗（例如執行指令時出現的終端機）取走時，點一下編輯視窗的標題列或狀態列左側把它帶回前景；
# 這些位置只會啟用視窗，不會觸發任何編輯動作。只點確實屬於編輯視窗的位置，找不到時回傳 false。
function Restore-EditorForeground([IntPtr]$Editor) {
    if ([Phase4Native]::GetForegroundWindow() -eq $Editor) { return $true }
    Assert-NoUserInput
    $rect = Get-WindowRect $Editor
    $candidates = @(
        @(($rect.X + [int]($rect.Width * 0.3)), ($rect.Y + 14)),
        @(($rect.X + [int]($rect.Width * 0.5)), ($rect.Y + 14)),
        @(($rect.X + 60), ($rect.Y + $rect.Height - 24)))
    foreach ($point in $candidates) {
        if ([Phase4Native]::RootWindowAt($point[0], $point[1]) -ne $Editor) { continue }
        [Phase4Native]::MoveCursor($point[0], $point[1])
        Start-Sleep -Milliseconds 60
        [void][Phase4Native]::LeftClick()
        if (Wait-Until { [Phase4Native]::GetForegroundWindow() -eq $Editor } 2000) {
            $script:foregroundRestores++
            Write-Host ("[INFO] {0:HH:mm:ss} 前景被其他視窗取走，已點編輯視窗帶回" -f (Get-Date))
            return $true
        }
    }
    return $false
}

function Get-AppProcesses { @(Get-Process -Name 'ScreenshotApp' -ErrorAction SilentlyContinue) }

function Start-App {
    $process = Start-Process -FilePath $ExePath -PassThru
    $ready = Wait-Until {
        $window = [IntPtr]::Zero; $id = [uint32]0
        [Phase4Native]::TryFindTrayIcon([uint32]$process.Id, [ref]$window, [ref]$id)
    } 8000
    return [pscustomobject]@{ Process = $process; TrayReady = [bool]$ready }
}

function Test-HotkeyRegistration {
    if ([Phase4Native]::RegisterHotKey([IntPtr]::Zero, 0x5301, $ModControl -bor $ModShift -bor $ModNoRepeat, [Phase4Native]::VK_2)) {
        [void][Phase4Native]::UnregisterHotKey([IntPtr]::Zero, 0x5301)
        return 0
    }
    return [Runtime.InteropServices.Marshal]::GetLastWin32Error()
}

# 只在快捷鍵確實由 ScreenshotApp 持有時送出：按鍵由系統攔截，不會落到前景的其他應用程式。
function Send-Hotkey {
    Assert-NoUserInput
    if ((Test-HotkeyRegistration) -ne $ErrorHotkeyAlreadyRegistered) { throw 'Ctrl+Shift+2 未被 ScreenshotApp 註冊，停止送出快捷鍵。' }
    [void][Phase4Native]::PressKeys([Phase4Native]::VK_CONTROL, [Phase4Native]::VK_SHIFT, [Phase4Native]::VK_2)
}

function Find-Windows([int]$ProcessId, [string]$Title) {
    @([Phase4Native]::TopLevelWindows([uint32]$ProcessId) |
        Where-Object { [Phase4Native]::IsWindowVisible($_) -and [Phase4Native]::TextOf($_) -eq $Title })
}

function Wait-Overlays([int]$ProcessId, [int]$Count) {
    Wait-Until { $overlays = @(Find-Windows $ProcessId $OverlayTitle); if ($overlays.Count -eq $Count) { , $overlays } } 5000
}

function Wait-NoOverlays([int]$ProcessId) { [bool](Wait-Until { @(Find-Windows $ProcessId $OverlayTitle).Count -eq 0 } 5000) }

function Find-Editor([int]$ProcessId) { Find-Windows $ProcessId $EditorTitle | Select-Object -First 1 }

function Wait-EditorForeground([int]$ProcessId) {
    Wait-Until { $editor = Find-Editor $ProcessId; if ($editor -and [Phase4Native]::GetForegroundWindow() -eq $editor) { $editor } } 5000
}

# 以 UI Automation 取得編輯視窗中的元素（AutomationId 即 XAML 的 x:Name）；找不到時回傳 $null。
function Find-EditorElement([IntPtr]$Editor, [string]$AutomationId, [int]$TimeoutMilliseconds = 3000) {
    # 變數不可命名為 $condition：Wait-Until 的參數同名，腳本區塊內會取到參數本身。
    $byId = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    Wait-Until { [System.Windows.Automation.AutomationElement]::FromHandle($Editor).FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byId) } $TimeoutMilliseconds
}

# 以 UI Automation 選擇工具按鈕，不送出按鍵（前景可能在檢查之後才被終端機取走）；回傳是否已選取。
function Select-Tool([IntPtr]$Editor, [string]$AutomationId) {
    Assert-NoUserInput
    $button = Find-EditorElement $Editor $AutomationId
    if (-not $button) { return $false }
    $pattern = $button.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $pattern.Select()
    return [bool](Wait-Until { $pattern.Current.IsSelected } 2000)
}

function Invoke-Drag([int]$FromX, [int]$FromY, [int]$ToX, [int]$ToY, [IntPtr[]]$Allowed) {
    Assert-Target $FromX $FromY $Allowed
    Assert-Target $ToX $ToY $Allowed
    [Phase4Native]::MoveCursor($FromX, $FromY)
    Start-Sleep -Milliseconds 60
    [void][Phase4Native]::LeftDown()
    Start-Sleep -Milliseconds 60
    foreach ($step in 1..4) {
        [Phase4Native]::MoveCursor([int]($FromX + ($ToX - $FromX) * $step / 4), [int]($FromY + ($ToY - $FromY) * $step / 4))
        Start-Sleep -Milliseconds 40
    }
    [void][Phase4Native]::LeftUp()
    Start-Sleep -Milliseconds 150
}

function Get-Pixels([System.Drawing.Bitmap]$Bitmap) {
    $rect = [System.Drawing.Rectangle]::new(0, 0, $Bitmap.Width, $Bitmap.Height)
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $bytes = [byte[]]::new($data.Stride * $Bitmap.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        return [pscustomobject]@{ Bytes = $bytes; Stride = $data.Stride; Width = $Bitmap.Width; Height = $Bitmap.Height }
    }
    finally { $Bitmap.UnlockBits($data) }
}

# 只用來檢查腳本自己的置頂圖樣視窗，不保存畫面。
function Get-ScreenPixels([int]$X, [int]$Y, [int]$Width, [int]$Height) {
    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.CopyFromScreen($X, $Y, 0, 0, $bitmap.Size) } finally { $graphics.Dispose() }
        return Get-Pixels $bitmap
    }
    finally { $bitmap.Dispose() }
}

function Get-WindowRect([IntPtr]$Window) {
    $rect = [Phase4Native+RECT]::new()
    [void][Phase4Native]::GetWindowRect($Window, [ref]$rect)
    return [pscustomobject]@{ X = $rect.Left; Y = $rect.Top; Width = $rect.Right - $rect.Left; Height = $rect.Bottom - $rect.Top }
}

# 以 PrintWindow 擷取編輯視窗本身的內容：不含其他重疊的視窗，也不會截到其他程式。
function Get-EditorPixels([IntPtr]$Editor, [string]$EvidenceName) {
    $rect = Get-WindowRect $Editor
    $bitmap = [System.Drawing.Bitmap]::new($rect.Width, $rect.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppRgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $hdc = $graphics.GetHdc()
            try { [void][Phase4Native]::PrintWindow($Editor, $hdc, 2) } finally { $graphics.ReleaseHdc($hdc) }
        }
        finally { $graphics.Dispose() }
        if ($EvidenceName) { $bitmap.Save((Join-Path $EvidenceDirectory $EvidenceName), [System.Drawing.Imaging.ImageFormat]::Png) }
        return [pscustomobject]@{ Rect = $rect; Pixels = Get-Pixels $bitmap }
    }
    finally { $bitmap.Dispose() }
}

# 在編輯視窗中尋找圖片上 (ImageX, ImageY) 起、未被標註覆蓋的區塊，回傳圖片原點 (0,0) 的螢幕座標；只在 100% 縮放時成立。
function Find-ImageOrigin([IntPtr]$Editor, [int]$ImageX, [int]$ImageY, [int]$Width, [int]$Height) {
    foreach ($attempt in 1..20) {
        $shot = Get-EditorPixels $Editor $null
        $fx = 0; $fy = 0
        $found = [Phase4Native]::FindPatternBlock($shot.Pixels.Bytes, $shot.Pixels.Stride, $shot.Pixels.Width, $shot.Pixels.Height,
            $Selection.X + $ImageX, $Selection.Y + $ImageY, $Width, $Height, [ref]$fx, [ref]$fy)
        if ($found) { return [pscustomobject]@{ X = $shot.Rect.X + $fx - $ImageX; Y = $shot.Rect.Y + $fy - $ImageY } }
        Start-Sleep -Milliseconds 150
    }
    return $null
}

function Invoke-Clipboard([scriptblock]$Action) {
    foreach ($attempt in 1..20) {
        try { return & $Action }
        catch [System.Runtime.InteropServices.ExternalException] { Start-Sleep -Milliseconds 50 }
    }
    throw '無法存取剪貼簿。'
}

function Test-SameImage($A, $B) {
    [bool]($A -and $B -and $A.Width -eq $B.Width -and $A.Height -eq $B.Height -and
        [Phase4Native]::CountDifferent($A.Bytes, $A.Stride, 0, 0, $B.Bytes, $B.Stride, 0, 0, $A.Width, $A.Height) -eq 0)
}

function Get-PatternWindows { @([Phase4Native]::TopLevelWindows([uint32]$patternProcess.Id) | Where-Object { [Phase4Native]::TextOf($_) -eq $PatternTitle }) }

function Hide-Pattern { foreach ($window in Get-PatternWindows) { [void][Phase4Native]::ShowWindow($window, [Phase4Native]::SW_HIDE) } }

# 顯示圖樣並等到螢幕上的圖樣完全正確，之後的截圖才有比對基準。
function Show-Pattern {
    foreach ($window in Get-PatternWindows) { [void][Phase4Native]::ShowWindow($window, [Phase4Native]::SW_SHOWNOACTIVATE) }
    return [bool](Wait-Until {
            $pixels = Get-ScreenPixels $Pattern.X $Pattern.Y $Pattern.Width $Pattern.Height
            $first = ''
            [Phase4Native]::CountPatternMismatches($pixels.Bytes, $pixels.Stride, 0, 0, $pixels.Width, $pixels.Height, 0, 0, [ref]$first) -eq 0
        } 8000)
}

# 截取圖樣上 $Selection 的範圍並等待編輯器出現在前景；回傳編輯視窗或 $null。
function Invoke-CaptureToEditor([int]$ProcessId) {
    if (-not (Show-Pattern)) { return $null }
    Send-Hotkey
    $overlays = Wait-Overlays $ProcessId $monitors.Count
    if (-not $overlays) { return $null }
    $fromX = $Pattern.X + $Selection.X
    $fromY = $Pattern.Y + $Selection.Y
    Invoke-Drag $fromX $fromY ($fromX + $Selection.Width - 1) ($fromY + $Selection.Height - 1) $overlays
    if (-not (Wait-NoOverlays $ProcessId)) { return $null }
    $editor = Wait-EditorForeground $ProcessId
    Hide-Pattern
    Start-Sleep -Milliseconds 400
    return $editor
}

function Send-TrayMessage([int]$ProcessId, [int]$MouseMessage) {
    foreach ($hWnd in [Phase4Native]::HiddenFormsWindows([uint32]$ProcessId)) {
        [void][Phase4Native]::PostMessage($hWnd, [Phase4Native]::WM_TRAYMOUSEMESSAGE, [IntPtr]1, [IntPtr]$MouseMessage)
    }
}

function Stop-AppViaTrayMenu([System.Diagnostics.Process]$Process) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::MenuItem)
    foreach ($attempt in 1..3) {
        Send-TrayMessage $Process.Id ([Phase4Native]::WM_RBUTTONUP)
        $menu = Wait-Until {
            foreach ($hWnd in [Phase4Native]::TopLevelWindows([uint32]$Process.Id)) {
                if ([Phase4Native]::IsWindowVisible($hWnd) -and [Phase4Native]::ClassOf($hWnd).StartsWith('WindowsForms10.Window')) { return $hWnd }
            }
        } 3000
        if (-not $menu) { continue }
        $item = @([System.Windows.Automation.AutomationElement]::FromHandle($menu).FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)) |
            Where-Object { $_.Current.Name -like '結束*' } | Select-Object -First 1
        if (-not $item) { continue }
        $item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return $Process.WaitForExit(8000)
    }
    return $false
}

$AppName = 'ScreenshotApp'
$ErrorTitle = 'ScreenshotApp 發生錯誤'
$SaveDialogTitle = '另存 PNG'
$OpenDialogTitle = '開啟圖片'

function Find-AppWindow([int]$ProcessId, [string]$Title) { Find-Windows $ProcessId $Title | Select-Object -First 1 }

function Wait-AppWindow([int]$ProcessId, [string]$Title, [int]$TimeoutMilliseconds = 5000) {
    Wait-Until { Find-AppWindow $ProcessId $Title } $TimeoutMilliseconds
}

# 以 UI Automation 按下 WPF 視窗中的按鈕（AutomationId 即 XAML 的 x:Name），不需要模擬輸入或前景。
function Invoke-Button([IntPtr]$Window, [string]$AutomationId) {
    Assert-NoUserInput
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($Window)
    # 變數不可命名為 $condition：Wait-Until 的參數同名，腳本區塊內會取到參數本身。
    $byId = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    $button = Wait-Until { $found = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byId); if ($found -and $found.Current.IsEnabled) { $found } } 5000
    if (-not $button) { Stop-Simulation "找不到可按下的按鈕 $AutomationId" }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 200
}

# 在檔案對話框的檔名欄填入完整路徑並按下預設按鈕（儲存或開啟）；以視窗訊息操作，不送出模擬按鍵。
function Complete-FileDialog([int]$ProcessId, [string]$Title, [string]$Path) {
    Assert-NoUserInput
    $dialog = Wait-AppWindow $ProcessId $Title 5000
    if (-not $dialog) { return $false }
    $edit = Wait-Until { $candidate = [Phase4Native]::FileNameEdit($dialog); if ($candidate -ne [IntPtr]::Zero) { $candidate } } 3000
    if (-not $edit) { return $false }
    [Phase4Native]::SetText($edit, $Path)
    [void][Phase4Native]::PostMessage($dialog, [Phase4Native]::WM_COMMAND, [IntPtr][Phase4Native]::IDOK, [IntPtr]::Zero)
    return [bool](Wait-Until { -not [Phase4Native]::IsWindow($dialog) } 5000)
}

# 在存檔對話框選擇唯讀檔。Windows 的存檔對話框可能自行拒絕（只有「確定」），也可能先詢問是否覆寫再交給受測程式寫入；
# 依序截圖並記錄出現的每個提示，回答「是」，沒有作用時改按「確定」；存檔對話框最後仍開著時以「取消」關閉。
function Invoke-ReadOnlySave([int]$ProcessId, [string]$Path) {
    $notes = [System.Collections.Generic.List[string]]::new()
    Assert-NoUserInput
    $dialog = Wait-AppWindow $ProcessId $SaveDialogTitle 5000
    if (-not $dialog) { return [pscustomobject]@{ Notes = @('沒有出現存檔對話框'); Cancelled = $false } }
    $edit = Wait-Until { $candidate = [Phase4Native]::FileNameEdit($dialog); if ($candidate -ne [IntPtr]::Zero) { $candidate } } 3000
    if (-not $edit) { return [pscustomobject]@{ Notes = @('找不到檔名欄'); Cancelled = $false } }
    [Phase4Native]::SetText($edit, $Path)
    [void][Phase4Native]::PostMessage($dialog, [Phase4Native]::WM_COMMAND, [IntPtr][Phase4Native]::IDOK, [IntPtr]::Zero)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ([Phase4Native]::IsWindow($dialog) -and $watch.ElapsedMilliseconds -lt 8000 -and $notes.Count -lt 3) {
        $prompt = [Phase4Native]::TopLevelWindows([uint32]$ProcessId) |
            Where-Object { [Phase4Native]::IsWindowVisible($_) -and [Phase4Native]::OwnerOf($_) -eq $dialog } | Select-Object -First 1
        if ($prompt) {
            Start-Sleep -Milliseconds 300
            $null = Get-EditorPixels $prompt ('phase4-readonly-prompt{0}.png' -f ($notes.Count + 1))
            $notes.Add((([Phase4Native]::TextOf($prompt) + '：' + [Phase4Native]::StaticText($prompt)) -replace '\s+', ' ').Trim())
            [Phase4Native]::ClickDialogButton($prompt, [Phase4Native]::IDYES)
            if (-not (Wait-Until { -not [Phase4Native]::IsWindow($prompt) } 1000)) { [Phase4Native]::ClickDialogButton($prompt, [Phase4Native]::IDOK) }
            [void](Wait-Until { -not [Phase4Native]::IsWindow($prompt) } 3000)
        }
        Start-Sleep -Milliseconds 200
    }
    $cancelled = $false
    if ([Phase4Native]::IsWindow($dialog)) {
        [void][Phase4Native]::PostMessage($dialog, [Phase4Native]::WM_COMMAND, [IntPtr][Phase4Native]::IDCANCEL, [IntPtr]::Zero)
        $cancelled = [bool](Wait-Until { -not [Phase4Native]::IsWindow($dialog) } 5000)
    }
    return [pscustomobject]@{ Notes = @($notes); Cancelled = $cancelled }
}

# 受測程式的錯誤訊息方塊與內文；沒有出現時回傳 $null。
function Wait-ErrorBox([int]$ProcessId, [int]$TimeoutMilliseconds = 5000) {
    $box = Wait-AppWindow $ProcessId $ErrorTitle $TimeoutMilliseconds
    if (-not $box) { return $null }
    Start-Sleep -Milliseconds 200
    return [pscustomobject]@{ Window = $box; Text = [Phase4Native]::StaticText($box) }
}

# 以「確定」關閉訊息方塊；沒有作用時改送 WM_CLOSE，仍未關閉才記錄診斷。
function Close-ErrorBox($Box) {
    [void][Phase4Native]::PostMessage($Box.Window, [Phase4Native]::WM_COMMAND, [IntPtr][Phase4Native]::IDOK, [IntPtr]::Zero)
    if (Wait-Until { -not [Phase4Native]::IsWindow($Box.Window) } 1500) { return }
    [void][Phase4Native]::PostMessage($Box.Window, [Phase4Native]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
    if (-not (Wait-Until { -not [Phase4Native]::IsWindow($Box.Window) } 3000)) {
        Add-Info '診斷：錯誤訊息方塊沒有關閉' (Describe-AppWindows $appId)
    }
}

# 受測程式所有可見的頂層視窗：標題、類別與是否啟用（被模態對話框停用的視窗為「停用」）。
function Describe-AppWindows([int]$ProcessId) {
    @([Phase4Native]::TopLevelWindows([uint32]$ProcessId) | Where-Object { [Phase4Native]::IsWindowVisible($_) } | ForEach-Object {
            '「{0}」{1}{2}' -f [Phase4Native]::TextOf($_), [Phase4Native]::ClassOf($_), $(if ([Phase4Native]::IsWindowEnabled($_)) { '' } else { '（停用）' })
        }) -join '；'
}

# 未匯出變更的詢問對話框（標題為程式名稱）。
function Wait-UnsavedDialog([int]$ProcessId, [int]$TimeoutMilliseconds = 5000) { Wait-AppWindow $ProcessId $AppName $TimeoutMilliseconds }

# 以指定的選項回答詢問：SaveButton、CopyButton、DiscardButton 或 CancelButton；回傳對話框是否已關閉。
function Resolve-UnsavedDialog([IntPtr]$Dialog, [string]$Button) {
    Invoke-Button $Dialog $Button
    return [bool](Wait-Until { -not [Phase4Native]::IsWindow($Dialog) } 3000)
}

# 剪貼簿上受測程式放入的兩種格式：PNG 位元組與像素，以及點陣圖（由 DIB 轉換）的像素；任一種不存在時回傳 $null。
function Read-ClipboardImages {
    Invoke-Clipboard {
        $data = [System.Windows.Forms.Clipboard]::GetDataObject()
        if (-not $data -or -not $data.GetDataPresent('PNG') -or -not $data.GetDataPresent([System.Windows.Forms.DataFormats]::Dib)) { return $null }
        $stream = $data.GetData('PNG')
        if ($stream -isnot [System.IO.MemoryStream]) { return $null }
        $bytes = $stream.ToArray()
        $png = [System.Drawing.Bitmap]::new([System.IO.MemoryStream]::new($bytes))
        $image = [System.Windows.Forms.Clipboard]::GetImage()
        try {
            $dib = [System.Drawing.Bitmap]::new($image)
            try { [pscustomobject]@{ PngBytes = $bytes; Png = Get-Pixels $png; Dib = Get-Pixels $dib } }
            finally { $dib.Dispose() }
        }
        finally {
            $png.Dispose()
            if ($image) { $image.Dispose() }
        }
    }
}

# 先放入文字，再按編輯器的「複製」按鈕，之後讀到的圖片一定是這次複製的結果。
function Copy-ViaButton([IntPtr]$Editor) {
    Invoke-Clipboard { [System.Windows.Forms.Clipboard]::SetText($Sentinel) }
    # 腳本自己寫入剪貼簿後立即記錄序號：之後即使在受測程式複製前停止，結束時仍會還原驗證前的備份。
    $script:ownClipboardSequence = [Phase4Native]::GetClipboardSequenceNumber()
    Invoke-Button $Editor 'CopyButton'
    $images = Wait-Until { Read-ClipboardImages } 8000
    Start-Sleep -Milliseconds 200
    $script:ownClipboardSequence = [Phase4Native]::GetClipboardSequenceNumber()
    return $images
}

# 以矩形工具在圖片 (X1, Y1)～(X2, Y2) 拖曳一個矩形（畫布為 100%）。工具以 UI Automation 選取，不送出按鍵；
# 各次拖曳的起點都離先前矩形的控制點夠遠，不需要先取消選取。
function Add-Rectangle([IntPtr]$Editor, $Origin, [int]$X1, [int]$Y1, [int]$X2, [int]$Y2) {
    if (-not (Select-Tool $Editor 'RectangleToolButton')) { Stop-Simulation '無法切換到矩形工具' }
    Invoke-Drag ($Origin.X + $X1) ($Origin.Y + $Y1) ($Origin.X + $X2) ($Origin.Y + $Y2) @($Editor)
}

# 以滑鼠右鍵取消選取：右鍵送到游標下的置頂覆蓋層，不受鍵盤焦點影響。
function Stop-OverlayByRightClick([int]$ProcessId) {
    $overlays = @(Find-Windows $ProcessId $OverlayTitle)
    $x = $Pattern.X + 40
    $y = $Pattern.Y + 40
    Assert-Target $x $y $overlays
    [Phase4Native]::MoveCursor($x, $y)
    Start-Sleep -Milliseconds 60
    [void][Phase4Native]::RightClick()
    return $true
}

# 開啟系統匣選單並以 UI Automation 選擇名稱符合的項目。
function Invoke-TrayMenuItem([System.Diagnostics.Process]$Process, [string]$NamePattern) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::MenuItem)
    foreach ($attempt in 1..3) {
        Assert-NoUserInput
        Send-TrayMessage $Process.Id ([Phase4Native]::WM_RBUTTONUP)
        $menu = Wait-Until {
            foreach ($hWnd in [Phase4Native]::TopLevelWindows([uint32]$Process.Id)) {
                if ([Phase4Native]::IsWindowVisible($hWnd) -and [Phase4Native]::ClassOf($hWnd).StartsWith('WindowsForms10.Window')) { return $hWnd }
            }
        } 3000
        if (-not $menu) { continue }
        $item = @([System.Windows.Automation.AutomationElement]::FromHandle($menu).FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)) |
            Where-Object { $_.Current.Name -like $NamePattern } | Select-Object -First 1
        if (-not $item) { continue }
        $item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return $true
    }
    return $false
}

# 產生開啟圖片用的測試檔：與圖樣相同編碼的 BMP 與 JPEG、只有檔頭與 IHDR 的 PNG（缺少影像資料的損壞檔；
# 若截斷在影像資料中途，Windows 的解碼器會以黑色補足而成功開啟），以及唯讀的 PNG（另存失敗的目標）。
function New-TestImages([string]$Directory) {
    $width = 320
    $height = 200
    $stride = 0
    $pixels = [Phase4Native]::PatternBgr24($width, $height, [ref]$stride)
    $bitmap = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $data = $bitmap.LockBits([System.Drawing.Rectangle]::new(0, 0, $width, $height), [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $bitmap.PixelFormat)
        try {
            for ($y = 0; $y -lt $height; $y++) {
                [System.Runtime.InteropServices.Marshal]::Copy($pixels, $y * $stride, [IntPtr]($data.Scan0.ToInt64() + $y * $data.Stride), $width * 3)
            }
        }
        finally { $bitmap.UnlockBits($data) }
        $paths = [pscustomobject]@{
            Width = $width; Height = $height
            Bmp = Join-Path $Directory 'open.bmp'; Jpeg = Join-Path $Directory 'open.jpg'
            Corrupt = Join-Path $Directory 'corrupt.png'; ReadOnly = Join-Path $Directory 'readonly.png'
        }
        $bitmap.Save($paths.Bmp, [System.Drawing.Imaging.ImageFormat]::Bmp)
        $bitmap.Save($paths.Jpeg, [System.Drawing.Imaging.ImageFormat]::Jpeg)
        $stream = [System.IO.MemoryStream]::new()
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $png = $stream.ToArray()
        [IO.File]::WriteAllBytes($paths.Corrupt, [byte[]]$png[0..32])
        [IO.File]::WriteAllBytes($paths.ReadOnly, $png)
        (Get-Item $paths.ReadOnly).IsReadOnly = $true
        return $paths
    }
    finally { $bitmap.Dispose() }
}

function Get-ErrorLogSince([long]$Length) {
    if (-not (Test-Path $LogPath)) { return '' }
    $stream = [IO.File]::Open($LogPath, 'Open', 'Read', 'ReadWrite')
    try {
        [void]$stream.Seek($Length, 'Begin')
        return [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8).ReadToEnd()
    }
    finally { $stream.Dispose() }
}

# 受測程式目前的資源用量：GDI 與 USER 物件數、Handle 數、私有記憶體與工作集。
function Get-ProcessSample([System.Diagnostics.Process]$Process, [int]$Cycle) {
    $Process.Refresh()
    [pscustomobject]@{
        Cycle = $Cycle
        Gdi = [Phase4Native]::GetGuiResources($Process.Handle, 0)
        User = [Phase4Native]::GetGuiResources($Process.Handle, 1)
        Handles = $Process.HandleCount
        PrivateMB = [math]::Round($Process.PrivateMemorySize64 / 1MB, 1)
        WorkingSetMB = [math]::Round($Process.WorkingSet64 / 1MB, 1)
    }
}

# 以 10 ms 間隔輪詢直到條件成立，回傳經過的毫秒數；逾時回傳 -1。量測值包含輪詢間隔與計時器解析度（約 16 ms）的誤差。
# 傳入的腳本區塊不可引用名為 $Probe、$Limit、$measureWatch 的變數（與本函式的變數同名時會取到本函式的值）。
function Measure-Until([scriptblock]$Probe, [int]$Limit = 10000) {
    $measureWatch = [Diagnostics.Stopwatch]::StartNew()
    while ($measureWatch.ElapsedMilliseconds -lt $Limit) {
        if (& $Probe) { return [int]$measureWatch.ElapsedMilliseconds }
        Start-Sleep -Milliseconds 10
    }
    return -1
}

# 編輯視窗中文字元素目前的內容（例如狀態列的圖片尺寸）；找不到時回傳空字串。
function Get-EditorText([IntPtr]$Editor, [string]$AutomationId) {
    $byId = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    $element = [System.Windows.Automation.AutomationElement]::FromHandle($Editor).FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byId)
    if ($element) { return $element.Current.Name }
    return ''
}

# 覆蓋層出現後等它取得前景：前景原本屬於其他程式（例如編輯器關閉後被啟用的視窗）時，覆蓋層顯示與取得前景之間有短暫的時間差。
function Wait-OverlayForeground([int]$ProcessId) {
    [bool](Wait-Until { (Find-Windows $ProcessId $OverlayTitle) -contains [Phase4Native]::GetForegroundWindow() } 3000)
}

# 截取圖樣上 (X, Y) 起 Width × Height 的範圍並量測：按下快捷鍵到所有覆蓋層出現、放開滑鼠到編輯器位於前景。
# 任一步驟逾時時 Editor 為 $null。
function Measure-CaptureToEditor([int]$ProcessId, [int]$X, [int]$Y, [int]$Width, [int]$Height) {
    $result = [pscustomobject]@{ OverlayMs = -1; EditorMs = -1; Editor = $null }
    if (-not (Show-Pattern)) { return $result }
    Send-Hotkey
    $result.OverlayMs = Measure-Until { @(Find-Windows $ProcessId $OverlayTitle).Count -eq $monitors.Count } 5000
    if ($result.OverlayMs -lt 0 -or -not (Wait-OverlayForeground $ProcessId)) { return $result }
    $overlays = @(Find-Windows $ProcessId $OverlayTitle)
    $toX = $X + $Width - 1
    $toY = $Y + $Height - 1
    Assert-Target $X $Y $overlays
    Assert-Target $toX $toY $overlays
    [Phase4Native]::MoveCursor($X, $Y)
    Start-Sleep -Milliseconds 60
    [void][Phase4Native]::LeftDown()
    Start-Sleep -Milliseconds 60
    foreach ($index in 1..4) {
        [Phase4Native]::MoveCursor([int]($X + ($toX - $X) * $index / 4), [int]($Y + ($toY - $Y) * $index / 4))
        Start-Sleep -Milliseconds 40
    }
    [void][Phase4Native]::LeftUp()
    $result.EditorMs = Measure-Until { $found = Find-Editor $ProcessId; [bool]$found -and [Phase4Native]::GetForegroundWindow() -eq $found } 8000
    if ($result.EditorMs -ge 0) { $result.Editor = Find-Editor $ProcessId }
    Hide-Pattern
    Start-Sleep -Milliseconds 400
    return $result
}

# 按下「複製」到剪貼簿更新的毫秒數：先放入文字並記錄序號，序號改變代表受測程式已寫入。
function Measure-Copy([IntPtr]$Editor) {
    Invoke-Clipboard { [System.Windows.Forms.Clipboard]::SetText($Sentinel) }
    $script:ownClipboardSequence = [Phase4Native]::GetClipboardSequenceNumber()
    $before = $script:ownClipboardSequence
    $button = Find-EditorElement $Editor 'CopyButton'
    if (-not $button) { return -1 }
    Assert-NoUserInput
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $elapsed = Measure-Until { [Phase4Native]::GetClipboardSequenceNumber() -ne $before } 10000
    Start-Sleep -Milliseconds 300
    $script:ownClipboardSequence = [Phase4Native]::GetClipboardSequenceNumber()
    return $elapsed
}

# 存檔對話框關閉後到 PNG 檔案出現的毫秒數（合成在 UI 執行緒，編碼與寫檔在背景執行緒，暫存檔完成後才改名為目標檔）。
function Measure-Save([int]$ProcessId, [IntPtr]$Editor, [string]$Path) {
    Invoke-Button $Editor 'SaveButton'
    if (-not (Complete-FileDialog $ProcessId $SaveDialogTitle $Path)) { return -1 }
    return Measure-Until { Test-Path -LiteralPath $Path } 15000
}

# PNG 檔頭記錄的寬高。
function Get-PngSize([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $width = ([int]$bytes[16] -shl 24) -bor ([int]$bytes[17] -shl 16) -bor ([int]$bytes[18] -shl 8) -bor [int]$bytes[19]
    $height = ([int]$bytes[20] -shl 24) -bor ([int]$bytes[21] -shl 16) -bor ([int]$bytes[22] -shl 8) -bor [int]$bytes[23]
    return '{0}×{1}' -f $width, $height
}

# 產生 Width × Height、與圖樣相同編碼的 PNG（開啟大圖片的延遲量測用）。
function New-PatternPng([string]$Path, [int]$Width, [int]$Height) {
    $stride = 0
    $pixels = [Phase4Native]::PatternBgr24($Width, $Height, [ref]$stride)
    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $data = $bitmap.LockBits([System.Drawing.Rectangle]::new(0, 0, $Width, $Height), [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $bitmap.PixelFormat)
        try {
            for ($row = 0; $row -lt $Height; $row++) {
                [System.Runtime.InteropServices.Marshal]::Copy($pixels, $row * $stride, [IntPtr]($data.Scan0.ToInt64() + $row * $data.Stride), $Width * 3)
            }
        }
        finally { $bitmap.UnlockBits($data) }
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $bitmap.Dispose() }
}

# 延遲量測值的摘要：中位數、最小、最大與有效次數。
function Format-Latency([int[]]$Values) {
    $valid = @($Values | Where-Object { $_ -ge 0 } | Sort-Object)
    if ($valid.Count -eq 0) { return '沒有量測值' }
    '中位數 {0} ms（最小 {1}、最大 {2}，{3} 次）' -f $valid[[math]::Floor(($valid.Count - 1) / 2)], $valid[0], $valid[-1], $valid.Count
}

# 關閉編輯器並等它消失；出現未匯出變更的詢問時選擇捨棄並回傳 'asked'。回傳 'closed'、'asked' 或 'open'。
function Close-EditorWindow([int]$ProcessId, [IntPtr]$Editor) {
    [void][Phase4Native]::PostMessage($Editor, [Phase4Native]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
    $state = Wait-Until { if (-not (Find-Editor $ProcessId)) { 'closed' } elseif (Find-AppWindow $ProcessId $AppName) { 'asked' } } 3000
    if ($state -eq 'asked') {
        [void](Resolve-UnsavedDialog (Find-AppWindow $ProcessId $AppName) 'DiscardButton')
        [void](Wait-Until { -not (Find-Editor $ProcessId) } 3000)
        return 'asked'
    }
    if ($state) { return $state }
    return 'open'
}

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$ExePath = (Resolve-Path $ExePath).Path
if (@(Get-AppProcesses).Count -gt 0) { throw '請先結束正在執行的 ScreenshotApp。' }

$idleWatch = [Diagnostics.Stopwatch]::StartNew()
while ([Phase4Native]::IdleMilliseconds() -lt $IdleSeconds * 1000) {
    if ($idleWatch.Elapsed.TotalMinutes -ge $MaxIdleWaitMinutes) {
        throw "等待 $MaxIdleWaitMinutes 分鐘仍有人在操作電腦，未開始驗證，也沒有做任何變更。"
    }
    Start-Sleep -Seconds 1
}
Write-Host ("[INFO] 已閒置 {0} 秒，開始驗證（等待 {1:N0} 秒）" -f $IdleSeconds, $idleWatch.Elapsed.TotalSeconds)

$dataDirectoryExisted = Test-Path $DataDirectory
$logDirectoryExisted = Test-Path $LogDirectory
$settingsBackup = $null
if (Test-Path $SettingsPath) {
    $settingsBackup = Join-Path $EvidenceDirectory 'settings.backup.json'
    Move-Item $SettingsPath $settingsBackup -Force
}
$logExisted = Test-Path $LogPath
$logLengthBefore = if ($logExisted) { (Get-Item $LogPath).Length } else { 0 }

$clipboardBackup = Invoke-Clipboard {
    $current = [System.Windows.Forms.Clipboard]::GetDataObject()
    $backup = [pscustomobject]@{ Data = [System.Windows.Forms.DataObject]::new(); Formats = @(); Skipped = @() }
    if ($current) {
        foreach ($format in $current.GetFormats($false)) {
            try {
                $value = $current.GetData($format, $false)
                if ($null -ne $value) { $backup.Data.SetData($format, $false, $value); $backup.Formats += $format }
                else { $backup.Skipped += $format }
            }
            catch { $backup.Skipped += $format }
        }
    }
    $backup
}
$clipboardFormats = @($clipboardBackup.Formats)
$clipboardSkipped = @($clipboardBackup.Skipped)
# 剪貼簿最後一次由本腳本（或受測程式依腳本指示）寫入後的序號；結束時序號不同代表使用者用過剪貼簿，不再覆蓋。
$script:ownClipboardSequence = [Phase4Native]::GetClipboardSequenceNumber()

$monitors = @([Phase4Native]::Monitors())
$primary = $monitors | Where-Object { $_[0] -eq 0 -and $_[1] -eq 0 } | Select-Object -First 1
$patternWidth = [math]::Min(1200, $primary[2] - 200)
$patternHeight = [math]::Min(800, $primary[3] - 200)
$Pattern = [pscustomobject]@{
    X = [int](($primary[2] - $patternWidth) / 2); Y = [int](($primary[3] - $patternHeight) / 2)
    Width = $patternWidth; Height = $patternHeight
}
# 每次截圖都選取圖樣上的同一塊範圍：圖片像素 (x, y) 即圖樣的 (Selection.X + x, Selection.Y + y)。
$Selection = [pscustomobject]@{ X = 100; Y = 80; Width = 600; Height = 400 }
$script:foregroundRestores = 0
$patternProcess = $null
$app = $null

try {
    Add-Info '顯示器' (($monitors | ForEach-Object { '({0},{1}) {2}×{3} {4}%' -f $_[0], $_[1], $_[2], $_[3], $_[4] }) -join '；')
    Add-Info '執行檔' ([IO.Path]::GetRelativePath($RepositoryRoot, $ExePath))
    Add-Info '剪貼簿備份' ("格式 {0} 種{1}" -f $clipboardFormats.Count, $(if ($clipboardSkipped) { '；無法讀取而不會還原：' + ($clipboardSkipped -join '、') } else { '' }))

    $files = Join-Path $EvidenceDirectory 'files'
    if (Test-Path $files) {
        Get-ChildItem $files -File | ForEach-Object { $_.IsReadOnly = $false }
        Remove-Item $files -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $files | Out-Null
    $test = New-TestImages $files
    $readOnlyBytes = [IO.File]::ReadAllBytes($test.ReadOnly)
    $savedPath = Join-Path $files ('saved_{0:yyyyMMdd_HHmmss}.png' -f (Get-Date))

    $patternProcess = Start-Process pwsh -PassThru -WindowStyle Hidden -ArgumentList @(
        '-NoProfile', '-File', (Join-Path $PSScriptRoot 'Show-TestPattern.ps1'),
        '-X', $Pattern.X, '-Y', $Pattern.Y, '-Width', $Pattern.Width, '-Height', $Pattern.Height)
    Add-Result '圖樣視窗與螢幕像素一致' (Show-Pattern) ('({0},{1}) {2}×{3}' -f $Pattern.X, $Pattern.Y, $Pattern.Width, $Pattern.Height)

    $app = Start-App
    $appId = $app.Process.Id
    Add-Result '啟動後常駐' ($app.TrayReady -and -not $app.Process.HasExited) "PID $appId"

    try {
        # 1. 截圖開啟編輯器
        $editor = Invoke-CaptureToEditor $appId
        Add-Result '選取完成後開啟編輯器' ([bool]$editor) $EditorTitle
        if (-not $editor) { Stop-Simulation '編輯器沒有開啟' }
        $origin = Find-ImageOrigin $editor 0 0 $Selection.Width $Selection.Height
        if (-not $origin) { Stop-Simulation '找不到畫布位置' }

        # 2. 複製：剪貼簿同時有 PNG 與點陣圖，兩者像素相同、尺寸等於截圖，且包含剛畫的矩形
        Add-Rectangle $editor $origin 40 40 239 139
        $copied = Copy-ViaButton $editor
        $copyOk = [bool]$copied -and $copied.Png.Width -eq $Selection.Width -and $copied.Png.Height -eq $Selection.Height -and (Test-SameImage $copied.Png $copied.Dib)
        Add-Result '複製：剪貼簿同時有 PNG 與點陣圖，像素相同且尺寸等於截圖' $copyOk $(if ($copied) { 'PNG {0}×{1}（{2:N0} 位元組）、點陣圖 {3}×{4}' -f $copied.Png.Width, $copied.Png.Height, $copied.PngBytes.Length, $copied.Dib.Width, $copied.Dib.Height } else { '剪貼簿缺少 PNG 或點陣圖' })
        $band = if ($copied) { [Phase4Native]::CountColor($copied.Png.Bytes, $copied.Png.Stride, 40, 40, 200, 3, $Red) } else { 0 }
        Add-Result '複製結果包含剛畫的矩形' ($band -eq 600) "上緣純紅 $band/600"

        # 3. 另存 PNG：檔案與之後複製的 PNG 逐位元組相同，並記住儲存位置
        Add-Rectangle $editor $origin 300 60 450 180
        Invoke-Button $editor 'SaveButton'
        $saved = (Complete-FileDialog $appId $SaveDialogTitle $savedPath) -and [bool](Wait-Until { Test-Path $savedPath } 8000)
        Start-Sleep -Milliseconds 500
        $afterSave = Copy-ViaButton $editor
        $sameBytes = $saved -and [bool]$afterSave -and [Linq.Enumerable]::SequenceEqual([byte[]][IO.File]::ReadAllBytes($savedPath), [byte[]]$afterSave.PngBytes)
        Add-Result '另存 PNG 與複製的 PNG 逐位元組相同' $sameBytes $(if ($saved) { '{0}（{1:N0} 位元組）' -f (Split-Path $savedPath -Leaf), (Get-Item $savedPath).Length } else { '檔案沒有寫入' })
        $lastDirectory = if (Test-Path $SettingsPath) { (Get-Content $SettingsPath -Raw | ConvertFrom-Json).LastSaveDirectory } else { '' }
        Add-Result '另存成功後記住儲存位置' ($lastDirectory -eq $files) "LastSaveDirectory = $lastDirectory"

        # 4. 另存到唯讀檔：由 Windows 存檔對話框拒絕，或由程式寫入失敗後顯示原因；兩種情況目標檔都不變、編輯器恢復可用
        Add-Rectangle $editor $origin 60 220 260 300
        Invoke-Button $editor 'SaveButton'
        $attempt = Invoke-ReadOnlySave $appId $test.ReadOnly
        $box = Wait-ErrorBox $appId 5000
        if ($box) { $null = Get-EditorPixels $box.Window 'phase4-save-failed.png'; Close-ErrorBox $box }
        $unchanged = [Linq.Enumerable]::SequenceEqual([byte[]][IO.File]::ReadAllBytes($test.ReadOnly), [byte[]]$readOnlyBytes)
        $usable = [bool](Wait-Until { [Phase4Native]::IsWindowEnabled($editor) } 3000)
        $refusedBy = if ($box) { '由 ScreenshotApp 顯示：' + ($box.Text -replace '\s+', ' ').Trim() } elseif ($attempt.Cancelled -and $attempt.Notes.Count -gt 0) { '由 Windows 存檔對話框拒絕，之後取消存檔' } else { '沒有出現任何原因' }
        $prompts = if ($attempt.Notes.Count -gt 0) { '；提示：' + ($attempt.Notes -join ' ／ ') } else { '' }
        Add-Result '另存到唯讀檔：顯示原因、目標檔不變、編輯器恢復可用' ((([bool]$box -and $box.Text -match '無法儲存圖片') -or ($attempt.Cancelled -and $attempt.Notes.Count -gt 0)) -and $unchanged -and $usable) ($refusedBy + $prompts)

        # 5. 剪貼簿被占用：腳本自己開啟剪貼簿，按「複製」後量測錯誤訊息出現的時間
        Assert-NoUserInput
        $held = [Phase4Native]::OpenClipboard([IntPtr]::Zero)
        $box = $null
        $elapsed = 0.0
        try {
            Invoke-Button $editor 'CopyButton'
            $watch = [Diagnostics.Stopwatch]::StartNew()
            $box = Wait-ErrorBox $appId 10000
            $elapsed = $watch.Elapsed.TotalSeconds
        }
        finally { [void][Phase4Native]::CloseClipboard() }
        if ($box) { $null = Get-EditorPixels $box.Window 'phase4-clipboard-busy.png'; Close-ErrorBox $box }
        Add-Result '剪貼簿被占用時顯示「正被其他程式使用」' ($held -and [bool]$box -and $box.Text -match '剪貼簿正被其他程式使用') $(if ($box) { '按下複製後約 {0:N1} 秒出現：{1}' -f $elapsed, ($box.Text -replace '\s+', ' ').Trim() } else { '沒有出現錯誤訊息' })

        # 6. 有未匯出的變更時按快捷鍵：先詢問；取消不截圖；捨棄後開始截圖，取消截圖回到原文件
        Send-Hotkey
        $dialog = Wait-UnsavedDialog $appId
        if ($dialog) { $null = Get-EditorPixels $dialog 'phase4-unsaved-dialog.png' }
        else {
            $null = Get-EditorPixels $editor 'phase4-no-unsaved-dialog.png'
            Add-Info '診斷：沒有出現詢問時的視窗' (Describe-AppWindows $appId)
        }
        $noOverlay = @(Find-Windows $appId $OverlayTitle).Count -eq 0
        $cancelled = $dialog -and (Resolve-UnsavedDialog $dialog 'CancelButton')
        Start-Sleep -Milliseconds 800
        Add-Result '新截圖前詢問；取消後不截圖、編輯器仍在' ([bool]$dialog -and $noOverlay -and $cancelled -and @(Find-Windows $appId $OverlayTitle).Count -eq 0 -and [Phase4Native]::IsWindowVisible($editor)) ''
        Send-Hotkey
        $dialog = Wait-UnsavedDialog $appId
        $discarded = $dialog -and (Resolve-UnsavedDialog $dialog 'DiscardButton')
        $opened = $discarded -and [bool](Wait-Overlays $appId $monitors.Count)
        $restored = $opened -and (Stop-OverlayByRightClick $appId) -and (Wait-NoOverlays $appId) -and [bool](Wait-Until { [Phase4Native]::IsWindowVisible($editor) } 3000)
        Add-Result '選擇捨棄後開始截圖；取消截圖後回到原編輯器' ([bool]$restored) ''

        # 7. 關閉編輯器前詢問（取消截圖不會改變文件，仍有未匯出的變更）；取消後編輯器仍開著
        [void][Phase4Native]::PostMessage($editor, [Phase4Native]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
        $dialog = Wait-UnsavedDialog $appId
        $kept = $dialog -and (Resolve-UnsavedDialog $dialog 'CancelButton')
        Start-Sleep -Milliseconds 500
        Add-Result '關閉編輯器前詢問；取消後編輯器仍開著' ($kept -and [bool](Find-Editor $appId)) ''

        # 8. 開啟圖片前詢問並選擇「複製後繼續」，再開啟 BMP：剪貼簿有複製結果，編輯器顯示與檔案逐像素相同的圖片
        Invoke-Clipboard { [System.Windows.Forms.Clipboard]::SetText($Sentinel) }
        $script:ownClipboardSequence = [Phase4Native]::GetClipboardSequenceNumber()
        Invoke-Button $editor 'OpenButton'
        $dialog = Wait-UnsavedDialog $appId
        $answered = $dialog -and (Resolve-UnsavedDialog $dialog 'CopyButton')
        $copiedFirst = $answered -and [bool](Wait-Until { Read-ClipboardImages } 8000)
        $script:ownClipboardSequence = [Phase4Native]::GetClipboardSequenceNumber()
        $openedBmp = $copiedFirst -and (Complete-FileDialog $appId $OpenDialogTitle $test.Bmp)
        Start-Sleep -Milliseconds 500
        $bmp = if ($openedBmp) { Copy-ViaButton $editor } else { $null }
        $first = ''
        $bmpMismatch = if ($bmp) { [Phase4Native]::CountPatternMismatches($bmp.Png.Bytes, $bmp.Png.Stride, 0, 0, $test.Width, $test.Height, 0, 0, [ref]$first) } else { -1 }
        Add-Result '開啟圖片前詢問，選擇複製後剪貼簿有複製結果' $copiedFirst ''
        Add-Result '開啟 BMP 後與檔案逐像素相同' ([bool]$bmp -and $bmp.Png.Width -eq $test.Width -and $bmp.Png.Height -eq $test.Height -and $bmpMismatch -eq 0) $(if ($bmp) { '{0}×{1}，不一致 {2} 個 {3}' -f $bmp.Png.Width, $bmp.Png.Height, $bmpMismatch, $first } else { '沒有開啟' })
        $null = Get-EditorPixels $editor 'phase4-opened-bmp.png'

        # 9. 開啟損壞的 PNG：顯示原因，原本的圖片不變（文件沒有變更，不詢問）
        Invoke-Button $editor 'OpenButton'
        $asked = [bool](Wait-UnsavedDialog $appId 1000)
        $openedCorrupt = Complete-FileDialog $appId $OpenDialogTitle $test.Corrupt
        $box = Wait-ErrorBox $appId 8000
        if ($box) { $null = Get-EditorPixels $box.Window 'phase4-open-failed.png'; Close-ErrorBox $box }
        $still = Copy-ViaButton $editor
        Add-Result '開啟損壞的 PNG 時顯示原因，原本的圖片不變' (-not $asked -and $openedCorrupt -and [bool]$box -and $box.Text -match '無法開啟' -and [bool]$still -and (Test-SameImage $still.Png $bmp.Png)) $(if ($box) { ($box.Text -replace '\s+', ' ').Trim() } else { '沒有出現錯誤訊息' })

        # 10. 開啟 JPEG
        Invoke-Button $editor 'OpenButton'
        $openedJpeg = Complete-FileDialog $appId $OpenDialogTitle $test.Jpeg
        Start-Sleep -Milliseconds 500
        $jpeg = if ($openedJpeg) { Copy-ViaButton $editor } else { $null }
        Add-Result '開啟 JPEG' ([bool]$jpeg -and $jpeg.Png.Width -eq $test.Width -and $jpeg.Png.Height -eq $test.Height) $(if ($jpeg) { '{0}×{1}' -f $jpeg.Png.Width, $jpeg.Png.Height } else { '沒有開啟' })

        # 11. 系統匣「結束」前詢問：先截一張新圖並加上矩形；取消後程式仍在，捨棄後結束
        $editor = Invoke-CaptureToEditor $appId
        $origin = if ($editor) { Find-ImageOrigin $editor 0 0 $Selection.Width $Selection.Height } else { $null }
        if (-not $origin) { Stop-Simulation '新截圖沒有開啟或找不到畫布位置' }
        Add-Rectangle $editor $origin 80 80 280 200
        $invoked = Invoke-TrayMenuItem $app.Process '結束*'
        $dialog = Wait-UnsavedDialog $appId
        $stay = $invoked -and $dialog -and (Resolve-UnsavedDialog $dialog 'CancelButton')
        Start-Sleep -Milliseconds 800
        Add-Result '系統匣「結束」前詢問；取消後程式仍在執行' ($stay -and -not $app.Process.HasExited) ''
        $invoked = Invoke-TrayMenuItem $app.Process '結束*'
        $dialog = Wait-UnsavedDialog $appId
        $left = $invoked -and $dialog -and (Resolve-UnsavedDialog $dialog 'DiscardButton')
        Add-Result '選擇捨棄後程式結束' ($left -and $app.Process.WaitForExit(8000)) "ExitCode=$(if ($app.Process.HasExited) { $app.Process.ExitCode } else { '未結束' })"

        # 12. 常駐時由系統匣「開啟圖片」：開啟編輯器；沒有變更時關閉編輯器與結束程式都不詢問
        $app = Start-App
        $appId = $app.Process.Id
        $invoked = Invoke-TrayMenuItem $app.Process '開啟圖片*'
        $openedFromTray = $invoked -and (Complete-FileDialog $appId $OpenDialogTitle $test.Bmp)
        $editor = if ($openedFromTray) { Wait-EditorForeground $appId } else { $null }
        $fromTray = if ($editor) { Copy-ViaButton $editor } else { $null }
        Add-Result '系統匣「開啟圖片」開啟編輯器並顯示圖片' ([bool]$fromTray -and (Test-SameImage $fromTray.Png $bmp.Png)) ''
        if ($editor) {
            [void][Phase4Native]::PostMessage($editor, [Phase4Native]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
            $closed = [bool](Wait-Until { -not (Find-Editor $appId) } 3000)
            Add-Result '沒有變更時關閉編輯器不詢問' ($closed -and -not (Find-AppWindow $appId $AppName)) ''
        }

        # 13. 連續完整循環：取消一次截圖、截圖開啟編輯器（量測延遲）、畫矩形、複製並驗證、另存 PNG、關閉編輯器；每次之後量測資源
        if ($Cycles -gt 0) {
            $cycleDirectory = Join-Path $files 'cycles'
            New-Item -ItemType Directory -Force -Path $cycleDirectory | Out-Null
            $samples = [System.Collections.Generic.List[object]]::new()
            $overlayTimes = [System.Collections.Generic.List[int]]::new()
            $editorTimes = [System.Collections.Generic.List[int]]::new()
            $failureNotes = [System.Collections.Generic.List[string]]::new()
            Start-Sleep -Milliseconds 500
            $samples.Add((Get-ProcessSample $app.Process 0))
            $cycleWatch = [Diagnostics.Stopwatch]::StartNew()
            for ($cycle = 1; $cycle -le $Cycles; $cycle++) {
                $stage = '取消截圖'
                $ok = Show-Pattern
                if ($ok) {
                    Send-Hotkey
                    $ok = [bool](Wait-Overlays $appId $monitors.Count) -and (Wait-OverlayForeground $appId) -and (Stop-OverlayByRightClick $appId) -and (Wait-NoOverlays $appId)
                }
                $cycleEditor = $null
                if ($ok) {
                    $stage = '截圖開啟編輯器'
                    $capture = Measure-CaptureToEditor $appId ($Pattern.X + $Selection.X) ($Pattern.Y + $Selection.Y) $Selection.Width $Selection.Height
                    $overlayTimes.Add($capture.OverlayMs)
                    $editorTimes.Add($capture.EditorMs)
                    $cycleEditor = $capture.Editor
                    $ok = [bool]$cycleEditor
                }
                $cycleOrigin = $null
                if ($ok) {
                    $stage = '尋找畫布'
                    $cycleOrigin = Find-ImageOrigin $cycleEditor 0 0 $Selection.Width $Selection.Height
                    $ok = [bool]$cycleOrigin
                }
                if ($ok) {
                    $stage = '複製'
                    $left = 30 + $cycle
                    Add-Rectangle $cycleEditor $cycleOrigin $left 30 ($left + 170) 150
                    $copiedCycle = Copy-ViaButton $cycleEditor
                    $ok = [bool]$copiedCycle -and $copiedCycle.Png.Width -eq $Selection.Width -and $copiedCycle.Png.Height -eq $Selection.Height -and
                        [Phase4Native]::CountColor($copiedCycle.Png.Bytes, $copiedCycle.Png.Stride, $left, 30, 100, 3, $Red) -eq 300
                }
                if ($ok) {
                    $stage = '另存'
                    $cyclePath = Join-Path $cycleDirectory ('cycle_{0:D2}.png' -f $cycle)
                    Invoke-Button $cycleEditor 'SaveButton'
                    $ok = (Complete-FileDialog $appId $SaveDialogTitle $cyclePath) -and [bool](Wait-Until { Test-Path -LiteralPath $cyclePath } 8000)
                    # 確認寫入後刪除，存檔資料夾的檔案數維持不變：存檔對話框列出的檔案（以及 Windows 為它們建立的圖示）
                    # 不隨循環次數增加，量到的才是 ScreenshotApp 自己的資源變化。
                    if ($ok) {
                        try { Remove-Item -LiteralPath $cyclePath -ErrorAction Stop }
                        catch { Start-Sleep -Milliseconds 500; Remove-Item -LiteralPath $cyclePath -ErrorAction SilentlyContinue }
                    }
                }
                if ($cycleEditor) {
                    Start-Sleep -Milliseconds 300
                    $closing = Close-EditorWindow $appId $cycleEditor
                    if ($ok -and $closing -ne 'closed') { $stage = "關閉編輯器（$closing）" }
                    $ok = $ok -and $closing -eq 'closed'
                }
                if (-not $ok) { $failureNotes.Add("第 $cycle 次：$stage") }
                Start-Sleep -Milliseconds 300
                $samples.Add((Get-ProcessSample $app.Process $cycle))
            }
            $samples | Export-Csv (Join-Path $EvidenceDirectory 'phase4-resources.csv') -NoTypeInformation -Encoding utf8
            Add-Result "連續 $Cycles 次完整循環皆成功" ($failureNotes.Count -eq 0) ("失敗 {0} 次{1}；共 {2:N0} 秒；每次 = 取消一次截圖、截圖開啟編輯器、畫矩形、複製並驗證、另存 PNG、關閉編輯器" -f $failureNotes.Count, $(if ($failureNotes.Count) { '（' + ($failureNotes -join '、') + '）' } else { '' }), $cycleWatch.Elapsed.TotalSeconds)
            $measured = @($samples | Where-Object Cycle -ge 1)
            $window = [math]::Max(1, [math]::Min(5, [math]::Floor($measured.Count / 2)))
            $head = @($measured | Select-Object -First $window)
            $tail = @($measured | Select-Object -Last $window)
            $describe = { param($name) '{0}：前 {1} 次 {2}～{3}，後 {1} 次 {4}～{5}' -f $name, $window,
                ($head | Measure-Object $name -Minimum).Minimum, ($head | Measure-Object $name -Maximum).Maximum,
                ($tail | Measure-Object $name -Minimum).Minimum, ($tail | Measure-Object $name -Maximum).Maximum }
            # 最小平方法的斜率：每次循環平均增加多少。
            $trend = { param($name)
                $meanCycle = ($measured | Measure-Object Cycle -Average).Average
                $meanValue = ($measured | Measure-Object $name -Average).Average
                $numerator = 0.0
                $denominator = 0.0
                foreach ($sample in $measured) {
                    $numerator += ($sample.Cycle - $meanCycle) * ($sample.$name - $meanValue)
                    $denominator += ($sample.Cycle - $meanCycle) * ($sample.Cycle - $meanCycle)
                }
                if ($denominator -eq 0) { 0.0 } else { $numerator / $denominator } }
            # 每次循環都開啟 Windows 存檔對話框，GDI 物件會因延後釋放出現單次的尖峰（下一次就回落），以前後各 10 次的中位數
            # 判斷基準是否上升；持續的洩漏會讓整體（含中位數）逐次增加。最小與最大值列於說明。
            $gdiWindow = [math]::Max(1, [math]::Min(10, [math]::Floor($measured.Count / 2)))
            $gdiHead = @($measured | Select-Object -First $gdiWindow | ForEach-Object Gdi | Sort-Object)
            $gdiTail = @($measured | Select-Object -Last $gdiWindow | ForEach-Object Gdi | Sort-Object)
            $gdiHeadMedian = $gdiHead[[math]::Floor(($gdiHead.Count - 1) / 2)]
            $gdiTailMedian = $gdiTail[[math]::Floor(($gdiTail.Count - 1) / 2)]
            Add-Result 'GDI 物件數沒有持續成長' ($gdiTailMedian -le $gdiHeadMedian + 2) ('Gdi：前 {0} 次中位數 {1}、後 {0} 次中位數 {2}（全部 {3}～{4}，每次 {5:+0.00;-0.00}）' -f
                $gdiWindow, $gdiHeadMedian, $gdiTailMedian, ($measured | Measure-Object Gdi -Minimum).Minimum, ($measured | Measure-Object Gdi -Maximum).Maximum, (& $trend 'Gdi'))
            Add-Result 'USER 物件數沒有成長' (($tail | Measure-Object User -Maximum).Maximum -le ($head | Measure-Object User -Maximum).Maximum + 2) (& $describe 'User')
            Add-Info 'Handle 與記憶體' ('{0}（每次 {1:+0.00;-0.00}）；{2} MB（每次 {3:+0.00;-0.00} MB）；{4} MB（每次 {5:+0.00;-0.00} MB）' -f
                (& $describe 'Handles'), (& $trend 'Handles'), (& $describe 'PrivateMB'), (& $trend 'PrivateMB'), (& $describe 'WorkingSetMB'), (& $trend 'WorkingSetMB'))
            Add-Info ('截圖延遲（選取 {0}×{1}）' -f $Selection.Width, $Selection.Height) ('按下快捷鍵到覆蓋層出現：{0}；放開滑鼠到編輯器位於前景：{1}' -f (Format-Latency $overlayTimes), (Format-Latency $editorTimes))
        }

        # 14. 延遲量測：FHD（1920×1080）截圖開啟編輯器、複製、另存；開啟 3840×2160 PNG、複製、另存。各 3 次。
        if ($primary[2] -ge 2000 -and $primary[3] -ge 1160) {
            if ($patternProcess -and -not $patternProcess.HasExited) {
                [void]$patternProcess.CloseMainWindow()
                if (-not $patternProcess.WaitForExit(3000)) { $patternProcess.Kill() }
            }
            $Pattern = [pscustomobject]@{ X = [int](($primary[2] - 1920) / 2); Y = [int](($primary[3] - 1080) / 2); Width = 1920; Height = 1080 }
            $patternProcess = Start-Process pwsh -PassThru -WindowStyle Hidden -ArgumentList @(
                '-NoProfile', '-File', (Join-Path $PSScriptRoot 'Show-TestPattern.ps1'),
                '-X', $Pattern.X, '-Y', $Pattern.Y, '-Width', $Pattern.Width, '-Height', $Pattern.Height)
            $fhdOverlay = [System.Collections.Generic.List[int]]::new()
            $fhdEditor = [System.Collections.Generic.List[int]]::new()
            $fhdCopy = [System.Collections.Generic.List[int]]::new()
            $fhdSave = [System.Collections.Generic.List[int]]::new()
            $fhdSize = ''
            foreach ($round in 1..3) {
                $capture = Measure-CaptureToEditor $appId $Pattern.X $Pattern.Y $Pattern.Width $Pattern.Height
                if (-not $capture.Editor) { Stop-Simulation 'FHD 截圖沒有開啟編輯器' }
                $fhdOverlay.Add($capture.OverlayMs)
                $fhdEditor.Add($capture.EditorMs)
                $fhdCopy.Add((Measure-Copy $capture.Editor))
                $fhdPath = Join-Path $files ('fhd_{0}.png' -f $round)
                $fhdSave.Add((Measure-Save $appId $capture.Editor $fhdPath))
                if (Test-Path -LiteralPath $fhdPath) { $fhdSize = Get-PngSize $fhdPath }
                [void](Close-EditorWindow $appId $capture.Editor)
            }
            Add-Info '截圖延遲（選取 1920×1080）' ('按下快捷鍵到覆蓋層出現：{0}；放開滑鼠到編輯器位於前景：{1}；按下複製到剪貼簿更新：{2}；存檔對話框關閉到檔案寫入完成：{3}；另存的 PNG {4}' -f
                (Format-Latency $fhdOverlay), (Format-Latency $fhdEditor), (Format-Latency $fhdCopy), (Format-Latency $fhdSave), $fhdSize)
        }
        else { Add-Info '截圖延遲（選取 1920×1080）' '主螢幕小於 2000×1160，未量測' }

        $largePath = Join-Path $files 'open_3840x2160.png'
        New-PatternPng $largePath 3840 2160
        $largeOpen = [System.Collections.Generic.List[int]]::new()
        $largeCopy = [System.Collections.Generic.List[int]]::new()
        $largeSave = [System.Collections.Generic.List[int]]::new()
        $largeSize = ''
        foreach ($round in 1..3) {
            if (-not (Invoke-TrayMenuItem $app.Process '開啟圖片*') -or -not (Complete-FileDialog $appId $OpenDialogTitle $largePath)) { Stop-Simulation '無法開啟 3840×2160 的圖片' }
            $largeOpen.Add((Measure-Until { $found = Find-Editor $appId; [bool]$found -and [Phase4Native]::GetForegroundWindow() -eq $found -and (Get-EditorText $found 'ImageSizeText') -eq '圖片：3840 × 2160' } 15000))
            $largeEditor = Find-Editor $appId
            if (-not $largeEditor) { Stop-Simulation '開啟 3840×2160 的圖片後沒有編輯器' }
            $largeCopy.Add((Measure-Copy $largeEditor))
            $largePathSaved = Join-Path $files ('large_{0}.png' -f $round)
            $largeSave.Add((Measure-Save $appId $largeEditor $largePathSaved))
            if (Test-Path -LiteralPath $largePathSaved) { $largeSize = Get-PngSize $largePathSaved }
            [void](Close-EditorWindow $appId $largeEditor)
        }
        Add-Info '開啟 3840×2160 PNG' ('開檔對話框關閉到編輯器顯示圖片：{0}；按下複製到剪貼簿更新：{1}；存檔對話框關閉到檔案寫入完成：{2}；另存的 PNG {3}' -f
            (Format-Latency $largeOpen), (Format-Latency $largeCopy), (Format-Latency $largeSave), $largeSize)
        $after = Get-ProcessSample $app.Process 0
        Add-Info '量測後資源' ('GDI {0}、USER {1}、Handle {2}、私有記憶體 {3} MB、工作集 {4} MB' -f $after.Gdi, $after.User, $after.Handles, $after.PrivateMB, $after.WorkingSetMB)
    }
    catch [System.OperationCanceledException] {
        Add-Result '驗證期間沒有使用者操作，模擬輸入完整執行' $false ("已停止模擬輸入：{0}" -f $_.Exception.Message)
    }
    Add-Info '前景恢復' ("沒有使用者操作時被其他視窗取走前景、點編輯視窗標題列或狀態列帶回 {0} 次" -f $script:foregroundRestores)

    if (-not $app.Process.HasExited) {
        Add-Result '系統匣「結束」後處理程序結束' (Stop-AppViaTrayMenu $app.Process) "ExitCode=$(if ($app.Process.HasExited) { $app.Process.ExitCode } else { '未結束' })"
    }
    $newLog = Get-ErrorLogSince $logLengthBefore
    Add-Result '過程中沒有新的錯誤記錄' ($newLog.Trim().Length -eq 0) $(if ($newLog) { $newLog.Substring(0, [math]::Min(300, $newLog.Length)) } else { '' })
}
finally {
    if ([Phase4Native]::CloseClipboard()) { Add-Info '剪貼簿' '結束前釋放了仍由腳本開啟的剪貼簿' }
    if ($app -and -not $app.Process.HasExited) { Get-AppProcesses | Stop-Process -Force -ErrorAction SilentlyContinue }
    if ($patternProcess -and -not $patternProcess.HasExited) {
        [void]$patternProcess.CloseMainWindow()
        if (-not $patternProcess.WaitForExit(3000)) { $patternProcess.Kill() }
    }
    if ($settingsBackup) { Move-Item $settingsBackup $SettingsPath -Force }
    elseif (Test-Path $SettingsPath) { Remove-Item $SettingsPath }
    if ($logExisted) {
        $stream = [IO.File]::Open($LogPath, 'Open', 'Write', 'ReadWrite')
        try { $stream.SetLength($logLengthBefore) } finally { $stream.Dispose() }
    }
    elseif (Test-Path $LogPath) { Remove-Item $LogPath }
    if (-not $logDirectoryExisted -and (Test-Path $LogDirectory) -and -not (Get-ChildItem $LogDirectory -Force)) { Remove-Item $LogDirectory }
    if (-not $dataDirectoryExisted -and (Test-Path $DataDirectory) -and -not (Get-ChildItem $DataDirectory -Force)) { Remove-Item $DataDirectory }

    # 剪貼簿在最後一次由腳本使用之後被其他程式改動時（使用者複製了東西），不覆蓋使用者的新內容。
    if ([Phase4Native]::GetClipboardSequenceNumber() -ne $script:ownClipboardSequence) {
        Add-Info '剪貼簿' '驗證期間有其他程式寫入剪貼簿，保留目前內容，不還原驗證前的備份'
    }
    elseif ($clipboardFormats.Count -gt 0) { Invoke-Clipboard { [System.Windows.Forms.Clipboard]::SetDataObject($clipboardBackup.Data, $true) } }
    else { Invoke-Clipboard { [System.Windows.Forms.Clipboard]::Clear() } }

    $results | ForEach-Object { "[{0}] {1} — {2}" -f $_.Status, $_.Check, $_.Detail } | Set-Content (Join-Path $EvidenceDirectory 'phase4-results.txt') -Encoding utf8
}

$failed = @($results | Where-Object Status -eq 'FAIL').Count
Write-Host ''
Write-Host ("合計：PASS {0}、FAIL {1}、INFO {2}；證據資料夾 {3}" -f @($results | Where-Object Status -eq 'PASS').Count, $failed, @($results | Where-Object Status -eq 'INFO').Count, $EvidenceDirectory)
exit $(if ($failed -eq 0) { 0 } else { 1 })
