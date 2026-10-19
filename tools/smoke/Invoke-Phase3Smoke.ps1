#Requires -Version 7
<#
.SYNOPSIS
    ScreenshotApp 圖片編輯器煙霧驗證：選取覆蓋層的十字游標、截圖後開啟編輯器、100% 時畫布與已知圖樣逐像素一致、以 Tab 巡覽控制項、繪製標註、文字輸入、
    復原與重做、選取刪除與移動、縮放後輸出不變、裁切（含裁切後填滿畫布）、預設樣式回寫、文件取代與恢復、重複循環的資源量測。

.DESCRIPTION
    會在目前的桌面工作階段實際啟動 ScreenshotApp，並以模擬的鍵盤與滑鼠操作；執行期間（約 2～4 分鐘）請勿操作滑鼠與鍵盤。
    截圖對象是腳本顯示的已知圖樣視窗（Show-TestPattern.ps1），編輯器開啟後先把圖樣視窗隱藏，避免置頂的圖樣蓋住編輯器。

    保護使用者的措施：
    - 開始前等使用者閒置；每次模擬輸入前檢查自上次模擬輸入後是否有使用者自己的鍵盤或滑鼠操作，有就立即停止，不再送出任何輸入。
    - 按鍵只在 ScreenshotApp 的視窗位於前景時送出；滑鼠操作前確認前景與目標位置的視窗都是 ScreenshotApp 自己的視窗。
    - 快捷鍵只在確認由 ScreenshotApp 註冊時送出（按鍵由系統攔截，不會落到其他應用程式）。
    - 編輯器畫面以 PrintWindow 只擷取 ScreenshotApp 的視窗內容，即使有其他視窗重疊也不會截到其他程式。
    - 結束時還原 settings.json 與故障日誌；剪貼簿只在驗證期間沒有被其他程式改動時才放回原本的內容。

.PARAMETER ExePath
    要驗證的 ScreenshotApp.exe；預設為 Release 建置輸出。

.PARAMETER EvidenceDirectory
    存放截圖、量測資料與結果清單的資料夾。

.PARAMETER Cycles
    資源量測的循環次數；每個循環包含截圖、繪製、複製、取消一次新截圖與關閉編輯器。

.PARAMETER IdleSeconds
    開始前需要的鍵盤與滑鼠閒置秒數；有人正在使用電腦時先等待，避免模擬輸入干擾使用者。

.PARAMETER MaxIdleWaitMinutes
    等待閒置的上限；超過時不做任何變更即結束。
#>
param(
    [string]$ExePath = (Join-Path $PSScriptRoot '..\..\ScreenshotApp\bin\Release\net10.0-windows\ScreenshotApp.exe'),
    [string]$EvidenceDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'ScreenshotApp.Smoke3'),
    [int]$Cycles = 10,
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

public static class Phase3Native
{
    public const int WM_CLOSE = 0x0010;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_TRAYMOUSEMESSAGE = 0x0400 + 1024;
    public const ushort VK_TAB = 0x09;
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
    public static uint RightClick() { return Send(MouseInput(0x0008, 0), MouseInput(0x0010, 0)); }

    // Ctrl+滾輪一格（direction 正值向上）：本機以 SendInput 送出的滾輪到不了編輯器，改為以 SendInput 按住 Ctrl、
    // 把 WM_MOUSEWHEEL 直接送給游標下的頂層視窗，再放開 Ctrl。張貼的訊息比輸入訊息先被取出，按下 Ctrl 後要先等編輯器處理；
    // WPF 依鍵盤狀態判斷 Ctrl，不看 wParam 的 MK_CONTROL。無論如何都會放開 Ctrl。
    public static void ControlWheelNotch(IntPtr window, int x, int y, int direction)
    {
        Send(KeyInput(VK_CONTROL, 0, 0));
        try
        {
            System.Threading.Thread.Sleep(120);
            var wParam = (IntPtr)(((direction > 0 ? 120 : -120) << 16) | 0x0008);
            var lParam = (IntPtr)((y << 16) | (x & 0xFFFF));
            PostMessage(window, 0x020A, wParam, lParam);
            System.Threading.Thread.Sleep(200);
        }
        finally
        {
            Send(KeyInput(VK_CONTROL, 0, 2));
        }
    }

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
}
'@

# 以 Per-Monitor V2 執行本腳本的座標計算與螢幕擷取，取得實體像素。
[void][Phase3Native]::SetThreadDpiAwarenessContext([IntPtr]-4)

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
    if ([Phase3Native]::UserInputSinceInjection(300)) { Stop-Simulation '偵測到使用者自己的鍵盤或滑鼠操作' }
}

# 滑鼠操作前確認前景與目標位置的視窗都屬於 ScreenshotApp（允許清單內的視窗）。
function Assert-Target([int]$X, [int]$Y, [IntPtr[]]$Allowed) {
    Assert-NoUserInput
    $foreground = [Phase3Native]::GetForegroundWindow()
    if ($Allowed -notcontains $foreground) {
        $editor = @($Allowed | Where-Object { [Phase3Native]::TextOf($_) -eq $EditorTitle }) | Select-Object -First 1
        if (-not $editor -or -not (Restore-EditorForeground $editor)) {
            Stop-Simulation ("前景視窗不是 ScreenshotApp（{0}）" -f [Phase3Native]::ClassOf($foreground))
        }
    }
    $root = [Phase3Native]::RootWindowAt($X, $Y)
    if ($Allowed -notcontains $root) { Stop-Simulation ("({0},{1}) 的視窗不是 ScreenshotApp（{2}）" -f $X, $Y, [Phase3Native]::ClassOf($root)) }
}

# 沒有使用者操作、前景卻被其他視窗（例如執行指令時出現的終端機）取走時，點一下編輯視窗的標題列或狀態列左側把它帶回前景；
# 這些位置只會啟用視窗，不會觸發任何編輯動作。只點確實屬於編輯視窗的位置，找不到時回傳 false。
function Restore-EditorForeground([IntPtr]$Editor) {
    if ([Phase3Native]::GetForegroundWindow() -eq $Editor) { return $true }
    Assert-NoUserInput
    $rect = Get-WindowRect $Editor
    $candidates = @(
        @(($rect.X + [int]($rect.Width * 0.3)), ($rect.Y + 14)),
        @(($rect.X + [int]($rect.Width * 0.5)), ($rect.Y + 14)),
        @(($rect.X + 60), ($rect.Y + $rect.Height - 24)))
    foreach ($point in $candidates) {
        if ([Phase3Native]::RootWindowAt($point[0], $point[1]) -ne $Editor) { continue }
        [Phase3Native]::MoveCursor($point[0], $point[1])
        Start-Sleep -Milliseconds 60
        [void][Phase3Native]::LeftClick()
        if (Wait-Until { [Phase3Native]::GetForegroundWindow() -eq $Editor } 2000) {
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
        [Phase3Native]::TryFindTrayIcon([uint32]$process.Id, [ref]$window, [ref]$id)
    } 8000
    return [pscustomobject]@{ Process = $process; TrayReady = [bool]$ready }
}

function Test-HotkeyRegistration {
    if ([Phase3Native]::RegisterHotKey([IntPtr]::Zero, 0x5301, $ModControl -bor $ModShift -bor $ModNoRepeat, [Phase3Native]::VK_2)) {
        [void][Phase3Native]::UnregisterHotKey([IntPtr]::Zero, 0x5301)
        return 0
    }
    return [Runtime.InteropServices.Marshal]::GetLastWin32Error()
}

# 只在快捷鍵確實由 ScreenshotApp 持有時送出：按鍵由系統攔截，不會落到前景的其他應用程式。
function Send-Hotkey {
    Assert-NoUserInput
    if ((Test-HotkeyRegistration) -ne $ErrorHotkeyAlreadyRegistered) { throw 'Ctrl+Shift+2 未被 ScreenshotApp 註冊，停止送出快捷鍵。' }
    [void][Phase3Native]::PressKeys([Phase3Native]::VK_CONTROL, [Phase3Native]::VK_SHIFT, [Phase3Native]::VK_2)
}

function Find-Windows([int]$ProcessId, [string]$Title) {
    @([Phase3Native]::TopLevelWindows([uint32]$ProcessId) |
        Where-Object { [Phase3Native]::IsWindowVisible($_) -and [Phase3Native]::TextOf($_) -eq $Title })
}

function Wait-Overlays([int]$ProcessId, [int]$Count) {
    Wait-Until { $overlays = @(Find-Windows $ProcessId $OverlayTitle); if ($overlays.Count -eq $Count) { , $overlays } } 5000
}

function Wait-NoOverlays([int]$ProcessId) { [bool](Wait-Until { @(Find-Windows $ProcessId $OverlayTitle).Count -eq 0 } 5000) }

function Find-Editor([int]$ProcessId) { Find-Windows $ProcessId $EditorTitle | Select-Object -First 1 }

function Wait-EditorForeground([int]$ProcessId) {
    Wait-Until { $editor = Find-Editor $ProcessId; if ($editor -and [Phase3Native]::GetForegroundWindow() -eq $editor) { $editor } } 5000
}

# 按鍵只在指定視窗位於前景時送出，否則停止驗證，避免按鍵落到其他應用程式。
function Send-KeysTo([IntPtr]$Window, [uint16[]]$Keys) {
    Assert-NoUserInput
    if (-not (Wait-Until { [Phase3Native]::GetForegroundWindow() -eq $Window } 1000) -and -not (Restore-EditorForeground $Window)) {
        Stop-Simulation 'ScreenshotApp 的視窗不在前景，停止送出按鍵'
    }
    [void][Phase3Native]::PressKeys($Keys)
    Start-Sleep -Milliseconds 150
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

# 在畫布上的文字輸入框填入文字：以 UI Automation 設定內容，不模擬打字，以免前景被取走時字元落到其他視窗。
# 輸入框沒有出現時回傳 $false，不做任何輸入。
function Set-EditorText([IntPtr]$Editor, [string]$Text) {
    Assert-NoUserInput
    $box = Find-EditorElement $Editor 'TextEditor' 2000
    if (-not $box -or $box.Current.IsOffscreen) { return $false }
    $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Text)
    Start-Sleep -Milliseconds 300
    return $true
}

# 狀態列顯示的縮放百分比，例如「400%」。
function Get-ZoomText([IntPtr]$Editor) {
    $text = Find-EditorElement $Editor 'ZoomPercentText'
    if ($text) { return $text.Current.Name }
    return ''
}

# 以 UI Automation 選擇屬性列的第 Index 個常用色（0 起算），不送出按鍵。
function Select-ColorItem([IntPtr]$Editor, [int]$Index) {
    Assert-NoUserInput
    $list = Find-EditorElement $Editor 'ColorList'
    if (-not $list) { return $false }
    $items = $list.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    if ($items.Count -le $Index) { return $false }
    $items[$Index].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    return $true
}

function Send-EscapeToOverlay([int]$ProcessId) {
    Assert-NoUserInput
    if (-not (Wait-Until { (Find-Windows $ProcessId $OverlayTitle) -contains [Phase3Native]::GetForegroundWindow() } 2000)) { Stop-Simulation '覆蓋層不在前景，停止送出 Esc' }
    [void][Phase3Native]::PressKeys([Phase3Native]::VK_ESCAPE)
    return $true
}

function Invoke-Drag([int]$FromX, [int]$FromY, [int]$ToX, [int]$ToY, [IntPtr[]]$Allowed) {
    Assert-Target $FromX $FromY $Allowed
    Assert-Target $ToX $ToY $Allowed
    [Phase3Native]::MoveCursor($FromX, $FromY)
    Start-Sleep -Milliseconds 60
    [void][Phase3Native]::LeftDown()
    Start-Sleep -Milliseconds 60
    foreach ($step in 1..4) {
        [Phase3Native]::MoveCursor([int]($FromX + ($ToX - $FromX) * $step / 4), [int]($FromY + ($ToY - $FromY) * $step / 4))
        Start-Sleep -Milliseconds 40
    }
    [void][Phase3Native]::LeftUp()
    Start-Sleep -Milliseconds 150
}

function Invoke-Click([int]$X, [int]$Y, [IntPtr[]]$Allowed) {
    Assert-Target $X $Y $Allowed
    [Phase3Native]::MoveCursor($X, $Y)
    Start-Sleep -Milliseconds 60
    [void][Phase3Native]::LeftClick()
    Start-Sleep -Milliseconds 150
}

# 在 (X, Y) 按滑鼠右鍵開啟編輯器的快顯選單，以 UI Automation 按下 AutomationId（XAML 的 x:Name）為 ItemId 的項目，等選單關閉後回傳 $true。
# 選單是 ScreenshotApp 處理程序另一個可見的頂層視窗；找不到選單或項目、或項目停用時送 Esc 關閉選單並回傳 $false。
function Invoke-ContextMenuItem([int]$X, [int]$Y, [string]$ItemId, [int]$ProcessId, [IntPtr]$Editor, [IntPtr[]]$Allowed) {
    Assert-Target $X $Y $Allowed
    [Phase3Native]::MoveCursor($X, $Y)
    Start-Sleep -Milliseconds 60
    [void][Phase3Native]::RightClick()
    # 變數不可命名為 $condition：Wait-Until 的參數同名，腳本區塊內會取到參數本身。
    $byId = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $ItemId)
    # 列舉途中出現或消失的工具提示等暫時視窗會讓 UI Automation 拋出例外，視為不是選單。
    $menuWindow = Wait-Until {
        [Phase3Native]::TopLevelWindows([uint32]$ProcessId) | Where-Object {
            $_ -ne $Editor -and [Phase3Native]::IsWindowVisible($_) -and
                $(try { [System.Windows.Automation.AutomationElement]::FromHandle($_).FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byId) } catch { $null })
        } | Select-Object -First 1
    } 3000
    $item = if ($menuWindow) { try { [System.Windows.Automation.AutomationElement]::FromHandle($menuWindow).FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byId) } catch { $null } }
    if (-not $item -or -not $item.Current.IsEnabled) {
        Send-KeysTo $Editor @([Phase3Native]::VK_ESCAPE)
        return $false
    }
    Assert-NoUserInput
    $item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    # 選單項目的動作在選單關閉之後才執行。
    [void](Wait-Until { -not [Phase3Native]::IsWindowVisible($menuWindow) } 2000)
    Start-Sleep -Milliseconds 300
    return $true
}

# 在 (X, Y) 一格一格以 Ctrl+滾輪縮放，直到狀態列顯示目標百分比（最多 12 格）；每一格之前都檢查使用者輸入與目標視窗。
function Invoke-ControlWheel([int]$X, [int]$Y, [int]$Direction, [string]$Target, [IntPtr]$Editor, [IntPtr[]]$Allowed) {
    for ($i = 0; $i -lt 12 -and (Get-ZoomText $Editor) -ne $Target; $i++) {
        Assert-Target $X $Y $Allowed
        [Phase3Native]::MoveCursor($X, $Y)
        Start-Sleep -Milliseconds 50
        [Phase3Native]::ControlWheelNotch([Phase3Native]::RootWindowAt($X, $Y), $X, $Y, $Direction)
        Start-Sleep -Milliseconds 250
    }
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
    $rect = [Phase3Native+RECT]::new()
    [void][Phase3Native]::GetWindowRect($Window, [ref]$rect)
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
            try { [void][Phase3Native]::PrintWindow($Editor, $hdc, 2) } finally { $graphics.ReleaseHdc($hdc) }
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
        $found = [Phase3Native]::FindPatternBlock($shot.Pixels.Bytes, $shot.Pixels.Stride, $shot.Pixels.Width, $shot.Pixels.Height,
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

# 先放入文字，再複製，之後讀到的圖片一定是這次複製的結果。回傳像素或 $null。
# 預設以 UI Automation 按下「複製」按鈕：前景可能在檢查之後才被終端機取走，以按鍵複製時 Ctrl+C 會落到終端機；
# 只有驗證快捷鍵本身時才指定 -UseShortcut。
function Copy-EditorImage([IntPtr]$Editor, [switch]$UseShortcut) {
    Assert-NoUserInput
    Invoke-Clipboard { [System.Windows.Forms.Clipboard]::SetText($Sentinel) }
    # 腳本自己寫入剪貼簿後立即記錄序號：之後即使在受測程式複製前停止，結束時仍會還原驗證前的備份。
    $script:ownClipboardSequence = [Phase3Native]::GetClipboardSequenceNumber()
    if ($UseShortcut) { Send-KeysTo $Editor @([Phase3Native]::VK_CONTROL, [uint16][char]'C') }
    else { Invoke-CopyButton $Editor }
    $image = Wait-Until { Invoke-Clipboard { if ([System.Windows.Forms.Clipboard]::ContainsImage()) { [System.Windows.Forms.Clipboard]::GetImage() } } } 5000
    # 受測程式放入剪貼簿後還會再提交一次內容，稍等再記錄序號。
    Start-Sleep -Milliseconds 200
    $script:ownClipboardSequence = [Phase3Native]::GetClipboardSequenceNumber()
    if (-not $image) { return $null }
    try {
        $bitmap = [System.Drawing.Bitmap]::new($image)
        try { return Get-Pixels $bitmap } finally { $bitmap.Dispose() }
    }
    finally { $image.Dispose() }
}

# 以 UI Automation 按下編輯器的「複製」按鈕（AutomationId 即 XAML 的 x:Name），不送出按鍵、不需要前景。
function Invoke-CopyButton([IntPtr]$Editor) {
    # 變數不可命名為 $condition：Wait-Until 的參數同名，腳本區塊內會取到參數本身。
    $byId = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'CopyButton')
    $button = Wait-Until {
        $found = [System.Windows.Automation.AutomationElement]::FromHandle($Editor).FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byId)
        if ($found -and $found.Current.IsEnabled) { $found }
    } 5000
    if (-not $button) { Stop-Simulation '找不到可按下的「複製」按鈕' }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Test-SameImage($A, $B) {
    [bool]($A -and $B -and $A.Width -eq $B.Width -and $A.Height -eq $B.Height -and
        [Phase3Native]::CountDifferent($A.Bytes, $A.Stride, 0, 0, $B.Bytes, $B.Stride, 0, 0, $A.Width, $A.Height) -eq 0)
}

function Get-PatternWindows { @([Phase3Native]::TopLevelWindows([uint32]$patternProcess.Id) | Where-Object { [Phase3Native]::TextOf($_) -eq $PatternTitle }) }

function Hide-Pattern { foreach ($window in Get-PatternWindows) { [void][Phase3Native]::ShowWindow($window, [Phase3Native]::SW_HIDE) } }

# 顯示圖樣並等到螢幕上的圖樣完全正確，之後的截圖才有比對基準。
function Show-Pattern {
    foreach ($window in Get-PatternWindows) { [void][Phase3Native]::ShowWindow($window, [Phase3Native]::SW_SHOWNOACTIVATE) }
    return [bool](Wait-Until {
            $pixels = Get-ScreenPixels $Pattern.X $Pattern.Y $Pattern.Width $Pattern.Height
            $first = ''
            [Phase3Native]::CountPatternMismatches($pixels.Bytes, $pixels.Stride, 0, 0, $pixels.Width, $pixels.Height, 0, 0, [ref]$first) -eq 0
        } 8000)
}

# 覆蓋層上顯示的游標只有黑白兩色（系統十字游標的反相像素在暗化後的白色畫面上看不見）、與系統游標同尺寸、熱點在中心。
function Test-OverlayCursor([int]$X, [int]$Y, [IntPtr[]]$Allowed) {
    Assert-Target $X $Y $Allowed
    [Phase3Native]::MoveCursor($X, $Y)
    $size = [Phase3Native]::GetSystemMetricsForDpi(13, [uint32]($primary[4] * 96 / 100))
    $cursor = Wait-Until { $current = [Phase3Native]::CurrentCursor(); if ($current -and $current[0] -eq $size) { , $current } } 2000
    if (-not $cursor) { $cursor = [Phase3Native]::CurrentCursor() }
    $center = [int]($size / 2) - 1
    $passed = [bool]$cursor -and $cursor[0] -eq $size -and $cursor[1] -eq $size -and $cursor[2] -eq $center -and $cursor[3] -eq $center -and
        $cursor[4] -gt 0 -and $cursor[5] -gt 0 -and $cursor[6] -eq 0
    $detail = if ($cursor) { '{0}×{1}，熱點 ({2},{3})，黑 {4}、白 {5}、反相 {6} 個像素；系統游標尺寸 {7}' -f $cursor[0], $cursor[1], $cursor[2], $cursor[3], $cursor[4], $cursor[5], $cursor[6], $size } else { '取不到單色游標' }
    Add-Result '覆蓋層游標為黑芯白邊十字（沒有反相像素、與系統游標同尺寸、熱點在中心）' $passed $detail
}

# 截取圖樣上 $Selection 的範圍並等待編輯器出現在前景；回傳編輯視窗或 $null。
function Invoke-CaptureToEditor([int]$ProcessId, [switch]$CheckCursor) {
    if (-not (Show-Pattern)) { return $null }
    Send-Hotkey
    $overlays = Wait-Overlays $ProcessId $monitors.Count
    if (-not $overlays) { return $null }
    $fromX = $Pattern.X + $Selection.X
    $fromY = $Pattern.Y + $Selection.Y
    if ($CheckCursor) { Test-OverlayCursor $fromX $fromY $overlays }
    Invoke-Drag $fromX $fromY ($fromX + $Selection.Width - 1) ($fromY + $Selection.Height - 1) $overlays
    if (-not (Wait-NoOverlays $ProcessId)) { return $null }
    $editor = Wait-EditorForeground $ProcessId
    Hide-Pattern
    Start-Sleep -Milliseconds 400
    return $editor
}

function Send-TrayMessage([int]$ProcessId, [int]$MouseMessage) {
    foreach ($hWnd in [Phase3Native]::HiddenFormsWindows([uint32]$ProcessId)) {
        [void][Phase3Native]::PostMessage($hWnd, [Phase3Native]::WM_TRAYMOUSEMESSAGE, [IntPtr]1, [IntPtr]$MouseMessage)
    }
}

function Stop-AppViaTrayMenu([System.Diagnostics.Process]$Process) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::MenuItem)
    foreach ($attempt in 1..3) {
        Send-TrayMessage $Process.Id ([Phase3Native]::WM_RBUTTONUP)
        $menu = Wait-Until {
            foreach ($hWnd in [Phase3Native]::TopLevelWindows([uint32]$Process.Id)) {
                if ([Phase3Native]::IsWindowVisible($hWnd) -and [Phase3Native]::ClassOf($hWnd).StartsWith('WindowsForms10.Window')) { return $hWnd }
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

function Get-Resources([System.Diagnostics.Process]$Process) {
    $Process.Refresh()
    [pscustomobject]@{
        Gdi = [Phase3Native]::GetGuiResources($Process.Handle, 0)
        User = [Phase3Native]::GetGuiResources($Process.Handle, 1)
        Handles = $Process.HandleCount
        PrivateMB = [math]::Round($Process.PrivateMemorySize64 / 1MB, 1)
    }
}

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$ExePath = (Resolve-Path $ExePath).Path
if (@(Get-AppProcesses).Count -gt 0) { throw '請先結束正在執行的 ScreenshotApp。' }

$idleWatch = [Diagnostics.Stopwatch]::StartNew()
while ([Phase3Native]::IdleMilliseconds() -lt $IdleSeconds * 1000) {
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
$script:ownClipboardSequence = [Phase3Native]::GetClipboardSequenceNumber()

$monitors = @([Phase3Native]::Monitors())
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

    $patternProcess = Start-Process pwsh -PassThru -WindowStyle Hidden -ArgumentList @(
        '-NoProfile', '-File', (Join-Path $PSScriptRoot 'Show-TestPattern.ps1'),
        '-X', $Pattern.X, '-Y', $Pattern.Y, '-Width', $Pattern.Width, '-Height', $Pattern.Height)
    Add-Result '圖樣視窗與螢幕像素一致' (Show-Pattern) ('({0},{1}) {2}×{3}' -f $Pattern.X, $Pattern.Y, $Pattern.Width, $Pattern.Height)

    $app = Start-App
    $appId = $app.Process.Id
    Add-Result '啟動後常駐' ($app.TrayReady -and -not $app.Process.HasExited) "PID $appId"

    try {
        # 1. 截圖完成後開啟編輯器，100% 時畫布與圖樣逐像素一致
        $editor = Invoke-CaptureToEditor $appId -CheckCursor
        Add-Result '選取完成後開啟編輯器並位於前景' ([bool]$editor) $EditorTitle
        if (-not $editor) { Stop-Simulation '編輯器沒有開啟' }
        $mine = @($editor)
        $origin = Find-ImageOrigin $editor 0 0 $Selection.Width $Selection.Height
        $null = Get-EditorPixels $editor 'phase3-editor-opened.png'
        Add-Result '開啟時以 100% 顯示且畫布與截圖逐像素一致' ([bool]$origin) $(if ($origin) { '圖片 {0}×{1} 位於螢幕 ({2},{3})，每個像素與圖樣相同' -f $Selection.Width, $Selection.Height, $origin.X, $origin.Y } else { '編輯視窗中找不到一比一的截圖' })
        if (-not $origin) { Stop-Simulation '找不到畫布位置' }
        function Screen([int]$ImageX, [int]$ImageY) { @(($origin.X + $ImageX), ($origin.Y + $ImageY)) }

        $plain = Copy-EditorImage $editor -UseShortcut
        $first = ''
        $plainMismatch = if ($plain) { [Phase3Native]::CountPatternMismatches($plain.Bytes, $plain.Stride, 0, 0, $plain.Width, $plain.Height, $Selection.X, $Selection.Y, [ref]$first) } else { -1 }
        Add-Result '未加標註時以 Ctrl+C 複製，結果與截圖一致' ([bool]$plain -and $plain.Width -eq $Selection.Width -and $plain.Height -eq $Selection.Height -and $plainMismatch -eq 0) $(if ($plain) { '{0}×{1}，與圖樣不一致 {2} 個 {3}' -f $plain.Width, $plain.Height, $plainMismatch, $first } else { '剪貼簿沒有圖片' })

        # 1b. 鍵盤巡覽：以 Tab 依序移動焦點，記錄 UI Automation 回報的焦點元素；工具列與屬性列中可用的控制項都要走得到。
        # 此時沒有選取任何標註、沒有復原紀錄：復原、重做與編號值是停用狀態，Tab 會略過。
        $visited = [System.Collections.Generic.List[string]]::new()
        for ($press = 0; $press -lt 40; $press++) {
            Send-KeysTo $editor @([Phase3Native]::VK_TAB)
            $focused = [System.Windows.Automation.AutomationElement]::FocusedElement
            $focusName = if (-not $focused) { '（無）' } elseif ($focused.Current.AutomationId) { $focused.Current.AutomationId } else { '{0}「{1}」' -f $focused.Current.ControlType.ProgrammaticName.Replace('ControlType.', ''), $focused.Current.Name }
            if ($visited.Count -gt 1 -and $focusName -eq $visited[0]) { break }
            $visited.Add($focusName)
        }
        $reachable = @('SelectToolButton', 'OpenButton', 'CopyButton', 'SaveButton', 'CustomColorButton', 'StrokeWidthBox', 'FontFamilyBox', 'FontSizeBox', 'ZoomBox')
        $unreached = @($reachable | Where-Object { $visited -notcontains $_ })
        Add-Result '以 Tab 巡覽可到達工具列、屬性列與縮放的控制項' ($unreached.Count -eq 0) ('焦點順序：{0}{1}' -f ($visited -join ' → '), $(if ($unreached) { '；未到達：' + ($unreached -join '、') } else { '' }))

        # 2. 矩形：以存取鍵 Alt+R 切換工具（其餘步驟以 UI Automation 切換），筆劃畫在範圍內側且為純紅色，其餘像素不變
        Send-KeysTo $editor @([Phase3Native]::VK_MENU, [uint16][char]'R')
        $rectangleTool = Find-EditorElement $editor 'RectangleToolButton'
        $accessKey = [bool]$rectangleTool -and [bool](Wait-Until { $rectangleTool.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected } 1000)
        Add-Result '存取鍵 Alt+R 切換到矩形工具' $accessKey ''
        if (-not $accessKey -and -not (Select-Tool $editor 'RectangleToolButton')) { Stop-Simulation '無法切換到矩形工具' }
        $a = Screen 40 40; $b = Screen 239 139
        Invoke-Drag $a[0] $a[1] $b[0] $b[1] $mine
        $withRectangle = Copy-EditorImage $editor
        if ($withRectangle) {
            $bandRed = [Phase3Native]::CountColor($withRectangle.Bytes, $withRectangle.Stride, 40, 40, 200, 3, $Red) +
                [Phase3Native]::CountColor($withRectangle.Bytes, $withRectangle.Stride, 40, 137, 200, 3, $Red) +
                [Phase3Native]::CountColor($withRectangle.Bytes, $withRectangle.Stride, 40, 43, 3, 94, $Red) +
                [Phase3Native]::CountColor($withRectangle.Bytes, $withRectangle.Stride, 237, 43, 3, 94, $Red)
            $expectedBand = 200 * 3 * 2 + 3 * 94 * 2
            $interior = [Phase3Native]::CountPatternMismatches($withRectangle.Bytes, $withRectangle.Stride, 43, 43, 194, 94, $Selection.X, $Selection.Y, [ref]$first)
            $outside = [Phase3Native]::CountPatternMismatches($withRectangle.Bytes, $withRectangle.Stride, 0, 0, 600, 39, $Selection.X, $Selection.Y, [ref]$first) +
                [Phase3Native]::CountPatternMismatches($withRectangle.Bytes, $withRectangle.Stride, 0, 140, 600, 260, $Selection.X, $Selection.Y, [ref]$first) +
                [Phase3Native]::CountPatternMismatches($withRectangle.Bytes, $withRectangle.Stride, 240, 39, 360, 101, $Selection.X, $Selection.Y, [ref]$first)
            Add-Result '矩形：筆劃在範圍內側且為純紅色、其餘像素不變' ($bandRed -eq $expectedBand -and $interior -eq 0 -and $outside -eq 0) ("筆劃純紅 {0}/{1}；內側不同 {2}；外側不同 {3}" -f $bandRed, $expectedBand, $interior, $outside)
        }
        else { Add-Result '矩形：筆劃在範圍內側且為純紅色、其餘像素不變' $false '剪貼簿沒有圖片' }

        # 3. 箭頭與步驟編號
        if (-not (Select-Tool $editor 'ArrowToolButton')) { Stop-Simulation '無法切換到箭頭工具' }
        $a = Screen 300 60; $b = Screen 450 180
        Invoke-Drag $a[0] $a[1] $b[0] $b[1] $mine
        if (-not (Select-Tool $editor 'StepToolButton')) { Stop-Simulation '無法切換到步驟編號工具' }
        foreach ($x in 480, 525, 570) { $p = Screen $x 60; Invoke-Click $p[0] $p[1] $mine }
        $withSteps = Copy-EditorImage $editor
        if ($withSteps) {
            # 已知圖樣在箭頭範圍內本來就有 560 個符合「偏紅」條件的像素，箭頭以「與原圖不同的像素」判斷；
            # 步驟編號的範圍內圖樣沒有偏紅的像素，仍以紅色像素判斷。
            $arrowChanged = [Phase3Native]::CountPatternMismatches($withSteps.Bytes, $withSteps.Stride, 296, 56, 160, 130, $Selection.X, $Selection.Y, [ref]$first)
            $stepRed = @(480, 525, 570 | ForEach-Object { [Phase3Native]::CountReddish($withSteps.Bytes, $withSteps.Stride, $_ - 15, 45, 31, 31) })
            Add-Result '箭頭出現在拖曳範圍' ($arrowChanged -gt 300) "與原圖不同的像素 $arrowChanged"
            Add-Result '步驟編號 1、2、3 依序放置' (@($stepRed | Where-Object { $_ -gt 300 }).Count -eq 3) ("各編號紅色像素 {0}" -f ($stepRed -join '、'))
        }
        $null = Get-EditorPixels $editor 'phase3-annotations.png'

        # 4. 復原與重做：Ctrl+Z 移除最後一個編號，Ctrl+Y 放回
        Send-KeysTo $editor @([Phase3Native]::VK_CONTROL, [uint16][char]'Z')
        $undone = Copy-EditorImage $editor
        $thirdGone = [bool]$undone -and [Phase3Native]::CountPatternMismatches($undone.Bytes, $undone.Stride, 553, 43, 35, 35, $Selection.X, $Selection.Y, [ref]$first) -eq 0
        Add-Result 'Ctrl+Z 復原最後一個步驟編號' $thirdGone '第三個編號所在範圍恢復為原圖'
        Send-KeysTo $editor @([Phase3Native]::VK_CONTROL, [uint16][char]'Y')
        Add-Result 'Ctrl+Y 重做後與復原前完全相同' (Test-SameImage (Copy-EditorImage $editor) $withSteps) ''

        # 5. 文字：輸入中與完成後的文字位置一致（比對範圍在文字左上角內側，不含輸入框外框、選取外框與插入點）
        if (-not (Select-Tool $editor 'TextToolButton')) { Stop-Simulation '無法切換到文字工具' }
        $p = Screen 60 220
        Invoke-Click $p[0] $p[1] $mine
        Start-Sleep -Milliseconds 300
        $typed = Set-EditorText $editor 'Hello 測試'
        Add-Result '文字工具點一下出現輸入框' $typed ''
        if (-not $typed) { Stop-Simulation '文字輸入框沒有出現，不輸入文字' }
        $editing = Get-EditorPixels $editor 'phase3-text-editing.png'
        Send-KeysTo $editor @([Phase3Native]::VK_CONTROL, [Phase3Native]::VK_RETURN)
        Start-Sleep -Milliseconds 300
        $committed = Get-EditorPixels $editor $null
        $tx = $origin.X - $editing.Rect.X + 60; $ty = $origin.Y - $editing.Rect.Y + 220
        # 文字範圍內的圖樣本身就有偏紅的像素，以「與原圖不同的像素」判斷文字是否出現（編輯視窗座標換算回圖樣座標）。
        $textPixels = [Phase3Native]::CountPatternMismatches($committed.Pixels.Bytes, $committed.Pixels.Stride, $tx, $ty, 80, 22,
            ($Selection.X - ($origin.X - $editing.Rect.X)), ($Selection.Y - ($origin.Y - $editing.Rect.Y)), [ref]$first)
        $moved = [Phase3Native]::CountDifferent($editing.Pixels.Bytes, $editing.Pixels.Stride, $tx, $ty, $committed.Pixels.Bytes, $committed.Pixels.Stride, $tx, $ty, 80, 22)
        Add-Result '文字輸入後顯示於畫布' ($textPixels -gt 100) "與原圖不同的像素 $textPixels"
        Add-Result '文字輸入中與完成後位置一致' ($moved -le 80 * 22 * 0.02) ("80×22 範圍內不同的像素 {0} 個" -f $moved)
        $withText = Copy-EditorImage $editor
        $exportedText = if ($withText) { [Phase3Native]::CountPatternMismatches($withText.Bytes, $withText.Stride, 60, 220, 120, 30, $Selection.X, $Selection.Y, [ref]$first) } else { 0 }
        Add-Result '文字出現在複製結果' ($exportedText -gt 100) "與原圖不同的像素 $exportedText"

        # 6. 選取並刪除箭頭，再復原
        if (-not (Select-Tool $editor 'SelectToolButton')) { Stop-Simulation '無法切換到選取工具' }
        $p = Screen 375 120
        Invoke-Click $p[0] $p[1] $mine
        Send-KeysTo $editor @([Phase3Native]::VK_DELETE)
        $deleted = Copy-EditorImage $editor
        # 箭頭的範圍（不含右側的步驟編號）完全恢復為原圖才算刪除。
        $arrowLeft = if ($deleted) { [Phase3Native]::CountPatternMismatches($deleted.Bytes, $deleted.Stride, 296, 56, 160, 130, $Selection.X, $Selection.Y, [ref]$first) } else { -1 }
        Add-Result '選取箭頭後按 Delete 刪除' ($arrowLeft -eq 0) $(if ($deleted) { "箭頭範圍與原圖不同的像素 $arrowLeft 個" } else { '剪貼簿沒有圖片' })
        Send-KeysTo $editor @([Phase3Native]::VK_CONTROL, [uint16][char]'Z')
        # 箭頭確實被刪除時才能驗證復原；沒有刪除時復原前後本來就相同，不能算通過。
        $restored = Test-SameImage (Copy-EditorImage $editor) $withText
        Add-Result '刪除後 Ctrl+Z 恢復箭頭' ($arrowLeft -eq 0 -and $restored) $(if ($arrowLeft -ne 0) { '箭頭沒有被刪除，無法驗證復原' } else { '' })

        # 7. 拖曳矩形的邊框移動 (20, 20)：一次拖曳一筆，Ctrl+Z 回到原位
        $a = Screen 41 90; $b = Screen 61 110
        Invoke-Drag $a[0] $a[1] $b[0] $b[1] $mine
        $movedImage = Copy-EditorImage $editor
        $movedRed = if ($movedImage) { [Phase3Native]::CountColor($movedImage.Bytes, $movedImage.Stride, 60, 60, 200, 3, $Red) } else { 0 }
        Add-Result '拖曳邊框移動矩形' ($movedRed -eq 600) "新位置上緣純紅像素 $movedRed/600"
        Send-KeysTo $editor @([Phase3Native]::VK_CONTROL, [uint16][char]'Z')
        Add-Result '移動只形成一筆操作（一次 Ctrl+Z 即回到原位）' (Test-SameImage (Copy-EditorImage $editor) $withText) ''

        # 8. 縮放 400% 與 25% 後複製，結果與 100% 完全相同
        Send-KeysTo $editor @([Phase3Native]::VK_ESCAPE)
        $center = Screen 300 200
        Invoke-ControlWheel $center[0] $center[1] 1 '400%' $editor $mine
        $zoomInText = Get-ZoomText $editor
        $null = Get-EditorPixels $editor 'phase3-zoom-400.png'
        $zoomed = Copy-EditorImage $editor
        Invoke-ControlWheel $center[0] $center[1] -1 '25%' $editor $mine
        $zoomOutText = Get-ZoomText $editor
        $null = Get-EditorPixels $editor 'phase3-zoom-25.png'
        $shrunk = Copy-EditorImage $editor
        Add-Result '縮放 400% 後複製結果與 100% 相同' ($zoomInText -eq '400%' -and (Test-SameImage $zoomed $withText)) "狀態列 $zoomInText"
        Add-Result '縮放 25% 後複製結果與 100% 相同' ($zoomOutText -eq '25%' -and (Test-SameImage $shrunk $withText)) "狀態列 $zoomOutText"
        Send-KeysTo $editor @([Phase3Native]::VK_CONTROL, [Phase3Native]::VK_0)
        Start-Sleep -Milliseconds 400
        $origin = Find-ImageOrigin $editor 300 300 200 80
        Add-Result 'Ctrl+0 回到符合視窗（100%）且位置可再次定位' ([bool]$origin) $(if ($origin) { '圖片原點 ({0},{1})' -f $origin.X, $origin.Y } else { '找不到一比一的區塊' })

        # 9. 裁切：輸出尺寸等於裁切範圍，內容為裁切前的對應區域；Ctrl+Z／Ctrl+Y。
        #    套用裁切後畫布以裁切範圍填滿（符合視窗可放大到 400%），復原回到整張圖片時重新符合視窗、不超過 100%。
        if ($origin) {
            if (-not (Select-Tool $editor 'CropToolButton')) { Stop-Simulation '無法切換到裁切工具' }
            Start-Sleep -Milliseconds 300
            $origin = Find-ImageOrigin $editor 300 300 200 80
            $a = Screen 20 20; $b = Screen 419 299
            Invoke-Drag $a[0] $a[1] $b[0] $b[1] $mine
            $null = Get-EditorPixels $editor 'phase3-crop-pending.png'
            Send-KeysTo $editor @([Phase3Native]::VK_RETURN)
            Start-Sleep -Milliseconds 400
            $null = Get-EditorPixels $editor 'phase3-cropped.png'
            $cropped = Copy-EditorImage $editor
            $cropMatches = [bool]$cropped -and $cropped.Width -eq 400 -and $cropped.Height -eq 280 -and
                [Phase3Native]::CountDifferent($cropped.Bytes, $cropped.Stride, 0, 0, $withText.Bytes, $withText.Stride, 20, 20, 400, 280) -eq 0
            Add-Result '裁切後輸出尺寸等於裁切範圍且內容一致' $cropMatches $(if ($cropped) { '{0}×{1}（預期 400×280）' -f $cropped.Width, $cropped.Height } else { '剪貼簿沒有圖片' })
            $croppedZoom = [int]((Get-ZoomText $editor) -replace '\D', '')
            Add-Result '套用裁切後以裁切範圍填滿畫布（縮放大於 100%）' ($croppedZoom -gt 100) "狀態列 $croppedZoom%"
            Send-KeysTo $editor @([Phase3Native]::VK_CONTROL, [uint16][char]'Z')
            $uncropped = Copy-EditorImage $editor
            Add-Result '裁切後 Ctrl+Z 恢復原尺寸' (Test-SameImage $uncropped $withText) $(if ($uncropped) { '{0}×{1}' -f $uncropped.Width, $uncropped.Height } else { '' })
            $uncroppedZoom = [int]((Get-ZoomText $editor) -replace '\D', '')
            Add-Result 'Ctrl+Z 後對整張圖片重新符合視窗（不超過 100%）' ($uncroppedZoom -gt 0 -and $uncroppedZoom -le 100) "狀態列 $uncroppedZoom%"
            Send-KeysTo $editor @([Phase3Native]::VK_CONTROL, [uint16][char]'Y')
            Add-Result '再以 Ctrl+Y 重做裁切' (Test-SameImage (Copy-EditorImage $editor) $cropped) ''
            $redoneZoom = [int]((Get-ZoomText $editor) -replace '\D', '')
            Add-Result 'Ctrl+Y 後再次以裁切範圍填滿畫布' ($redoneZoom -eq $croppedZoom) "狀態列 $redoneZoom%（套用時 $croppedZoom%）"

            # 9b. 在畫布空白處按右鍵，以畫布選單的「還原完整圖片」回到整張圖片並重新符合視窗；Ctrl+Z 恢復裁切，後續步驟的狀態不變。
            #     位置取視窗水平中央、狀態列上方（視窗下緣往上 70 個實體像素）：符合視窗時畫布置中，175% 時落在裁切範圍下方的
            #     灰色邊界（對應圖片 y 300 以下），100% 時落在裁切範圍下緣內側的 x 220 附近；兩處都沒有標註（矩形在 y 139 以上、
            #     箭頭在 x 300 以右、文字在 y 247 以上）。點到標註時開啟的是標註選單，找不到「還原完整圖片」而判定失敗。
            $rect = Get-WindowRect $editor
            $reset = Invoke-ContextMenuItem ($rect.X + [int]($rect.Width / 2)) ($rect.Y + $rect.Height - 70) 'CanvasResetCropMenuItem' $appId $editor $mine
            $restored = if ($reset) { Copy-EditorImage $editor } else { $null }
            $resetZoom = [int]((Get-ZoomText $editor) -replace '\D', '')
            Add-Result '右鍵畫布選單「還原完整圖片」：輸出回到整張圖片並重新符合視窗' ($reset -and (Test-SameImage $restored $withText) -and $resetZoom -gt 0 -and $resetZoom -le 100) $(if ($reset) { "狀態列 $resetZoom%" } else { '選單或項目沒有出現' })
            if ($reset) {
                Send-KeysTo $editor @([Phase3Native]::VK_CONTROL, [uint16][char]'Z')
                Add-Result '還原完整圖片後 Ctrl+Z 恢復裁切' (Test-SameImage (Copy-EditorImage $editor) $cropped) ''
            }
        }

        # 10. 沒有選取時變更顏色，寫入設定檔成為預設值；再改回紅色
        Send-KeysTo $editor @([Phase3Native]::VK_ESCAPE)
        $picked = Select-ColorItem $editor 1
        Start-Sleep -Milliseconds 400
        $saved = if (Test-Path $SettingsPath) { (Get-Content $SettingsPath -Raw | ConvertFrom-Json).DefaultColor } else { '' }
        Add-Result '未選取時變更顏色寫入設定檔' ($picked -and $saved -eq '#FF8C00') "DefaultColor = $saved"
        [void](Select-ColorItem $editor 0)
        Start-Sleep -Milliseconds 300

        # 11. 編輯中取消新截圖：恢復原文件與視窗；完成新截圖：以新文件取代
        $before = Copy-EditorImage $editor
        if (Show-Pattern) {
            Send-Hotkey
            $opened = Wait-Overlays $appId $monitors.Count
            Add-Result '編輯中按快捷鍵：編輯器隱藏並開啟覆蓋層' ([bool]$opened -and -not [Phase3Native]::IsWindowVisible($editor)) ''
            $restored = $opened -and (Send-EscapeToOverlay $appId) -and (Wait-NoOverlays $appId) -and [bool](Wait-Until { [Phase3Native]::IsWindowVisible($editor) } 3000)
            Hide-Pattern
            Start-Sleep -Milliseconds 300
            $after = if ($restored) { Copy-EditorImage $editor } else { $null }
            Add-Result '取消新截圖後恢復原編輯視窗與文件' ([bool]$restored -and (Test-SameImage $after $before)) ''
        }
        $replaced = Invoke-CaptureToEditor $appId
        $fresh = if ($replaced) { Copy-EditorImage $replaced } else { $null }
        $freshMismatch = if ($fresh) { [Phase3Native]::CountPatternMismatches($fresh.Bytes, $fresh.Stride, 0, 0, $fresh.Width, $fresh.Height, $Selection.X, $Selection.Y, [ref]$first) } else { -1 }
        Add-Result '完成新截圖後以新文件取代（同一個編輯視窗）' ($replaced -eq $editor -and [bool]$fresh -and $fresh.Width -eq $Selection.Width -and $freshMismatch -eq 0) ("與圖樣不一致 {0} 個" -f $freshMismatch)

        # 12. 編輯中再次啟動程式：第二個執行個體結束並帶出編輯視窗
        Assert-NoUserInput
        $second = Start-Process -FilePath $ExePath -PassThru
        $secondExited = $second.WaitForExit(8000)
        Add-Result '編輯中再次啟動：第二個執行個體結束且編輯視窗在前景' ($secondExited -and [bool](Wait-EditorForeground $appId) -and @(Get-AppProcesses).Count -eq 1) ("ExitCode={0}" -f $(if ($secondExited) { $second.ExitCode } else { '未結束' }))

        # 13. 關閉編輯器回到常駐
        [void][Phase3Native]::PostMessage($editor, [Phase3Native]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
        $closed = [bool](Wait-Until { -not (Find-Editor $appId) } 3000)
        Add-Result '關閉編輯器後程式仍常駐' ($closed -and -not $app.Process.HasExited -and (Test-HotkeyRegistration) -eq $ErrorHotkeyAlreadyRegistered) ''

        # 14. 重複循環：截圖 → 繪製 → 複製 → 取消一次新截圖 → 關閉編輯器
        $samples = [System.Collections.Generic.List[object]]::new()
        $baseline = Get-Resources $app.Process
        $samples.Add([pscustomobject]@{ Cycle = 0; Gdi = $baseline.Gdi; User = $baseline.User; Handles = $baseline.Handles; PrivateMB = $baseline.PrivateMB })
        $failures = 0
        foreach ($cycle in 1..$Cycles) {
            $ok = $false
            $cycleEditor = Invoke-CaptureToEditor $appId
            if ($cycleEditor) {
                $cycleOrigin = Find-ImageOrigin $cycleEditor 0 0 $Selection.Width $Selection.Height
                if ($cycleOrigin) {
                    [void](Select-Tool $cycleEditor 'RectangleToolButton')
                    Invoke-Drag ($cycleOrigin.X + 30 + $cycle) ($cycleOrigin.Y + 30) ($cycleOrigin.X + 200 + $cycle) ($cycleOrigin.Y + 150) @($cycleEditor)
                    $copied = Copy-EditorImage $cycleEditor
                    $ok = [bool]$copied -and $copied.Width -eq $Selection.Width -and [Phase3Native]::CountColor($copied.Bytes, $copied.Stride, 30 + $cycle, 30, 100, 3, $Red) -eq 300
                }
                if ($ok -and (Show-Pattern)) {
                    Send-Hotkey
                    $ok = [bool](Wait-Overlays $appId $monitors.Count) -and (Send-EscapeToOverlay $appId) -and (Wait-NoOverlays $appId) -and
                        [bool](Wait-Until { [Phase3Native]::IsWindowVisible($cycleEditor) } 3000)
                    Hide-Pattern
                }
                [void][Phase3Native]::PostMessage($cycleEditor, [Phase3Native]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
                $ok = $ok -and [bool](Wait-Until { -not (Find-Editor $appId) } 3000)
            }
            if (-not $ok) { $failures++ }
            Start-Sleep -Milliseconds 300
            $resources = Get-Resources $app.Process
            $samples.Add([pscustomobject]@{ Cycle = $cycle; Gdi = $resources.Gdi; User = $resources.User; Handles = $resources.Handles; PrivateMB = $resources.PrivateMB })
        }
        $samples | Export-Csv (Join-Path $EvidenceDirectory 'phase3-resources.csv') -NoTypeInformation -Encoding utf8
        Add-Result "連續 $Cycles 次編輯循環皆成功" ($failures -eq 0) "失敗 $failures 次；每次 = 截圖開啟編輯器、畫矩形、複製並驗證、取消一次新截圖、關閉編輯器"
        $measured = @($samples | Where-Object Cycle -ge 1)
        $window = [math]::Max(1, [math]::Min(5, [math]::Floor($measured.Count / 2)))
        $head = @($measured | Select-Object -First $window)
        $tail = @($measured | Select-Object -Last $window)
        $describe = { param($name) '{0}：前 {1} 次 {2}～{3}，後 {1} 次 {4}～{5}' -f $name, $window,
            ($head | Measure-Object $name -Minimum).Minimum, ($head | Measure-Object $name -Maximum).Maximum,
            ($tail | Measure-Object $name -Minimum).Minimum, ($tail | Measure-Object $name -Maximum).Maximum }
        Add-Result 'GDI 物件數沒有成長' (($tail | Measure-Object Gdi -Maximum).Maximum -le ($head | Measure-Object Gdi -Maximum).Maximum) (& $describe 'Gdi')
        Add-Result 'USER 物件數沒有成長' (($tail | Measure-Object User -Maximum).Maximum -le ($head | Measure-Object User -Maximum).Maximum + 2) (& $describe 'User')
        Add-Info 'Handle 與私有記憶體' ((& $describe 'Handles') + '；' + (& $describe 'PrivateMB') + ' MB')
    }
    catch [System.OperationCanceledException] {
        Add-Result '驗證期間沒有使用者操作，模擬輸入完整執行' $false ("已停止模擬輸入：{0}" -f $_.Exception.Message)
    }
    Add-Info '前景恢復' ("沒有使用者操作時被其他視窗取走前景、點編輯視窗標題列或狀態列帶回 {0} 次" -f $script:foregroundRestores)

    # 15. 由系統匣「結束」；故障日誌沒有新記錄
    Add-Result '系統匣「結束」後處理程序結束' (Stop-AppViaTrayMenu $app.Process) "ExitCode=$(if ($app.Process.HasExited) { $app.Process.ExitCode } else { '未結束' })"
    $newLog = ''
    if (Test-Path $LogPath) {
        $stream = [IO.File]::Open($LogPath, 'Open', 'Read', 'ReadWrite')
        try {
            [void]$stream.Seek($logLengthBefore, 'Begin')
            $newLog = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8).ReadToEnd()
        }
        finally { $stream.Dispose() }
    }
    Add-Result '過程中沒有新的錯誤記錄' ($newLog.Trim().Length -eq 0) $(if ($newLog) { $newLog.Substring(0, [math]::Min(300, $newLog.Length)) } else { '' })
}
finally {
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
    if ([Phase3Native]::GetClipboardSequenceNumber() -ne $script:ownClipboardSequence) {
        Add-Info '剪貼簿' '驗證期間有其他程式寫入剪貼簿，保留目前內容，不還原驗證前的備份'
    }
    elseif ($clipboardFormats.Count -gt 0) { Invoke-Clipboard { [System.Windows.Forms.Clipboard]::SetDataObject($clipboardBackup.Data, $true) } }
    else { Invoke-Clipboard { [System.Windows.Forms.Clipboard]::Clear() } }

    $results | ForEach-Object { "[{0}] {1} — {2}" -f $_.Status, $_.Check, $_.Detail } | Set-Content (Join-Path $EvidenceDirectory 'phase3-results.txt') -Encoding utf8
}

$failed = @($results | Where-Object Status -eq 'FAIL').Count
Write-Host ''
Write-Host ("合計：PASS {0}、FAIL {1}、INFO {2}；證據資料夾 {3}" -f @($results | Where-Object Status -eq 'PASS').Count, $failed, @($results | Where-Object Status -eq 'INFO').Count, $EvidenceDirectory)
exit $(if ($failed -eq 0) { 0 } else { 1 })
