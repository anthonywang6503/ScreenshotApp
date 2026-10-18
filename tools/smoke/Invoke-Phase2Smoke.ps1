#Requires -Version 7
<#
.SYNOPSIS
    ScreenshotApp 截圖與區域選取煙霧驗證：覆蓋層貼合顯示器、選取結果與已知圖樣逐像素一致、取消、零尺寸、重入、
    自家視窗與系統匣選單不被截入、顯示設定變更、重複循環的資源量測。

.DESCRIPTION
    會在目前的桌面工作階段實際啟動 ScreenshotApp，並以模擬的鍵盤與滑鼠操作截圖；執行期間（約 2～3 分鐘）請勿操作滑鼠與鍵盤。
    腳本在主螢幕中央顯示一個已知圖樣的置頂視窗（Show-TestPattern.ps1），所有選取都在圖樣範圍內進行，
    證據截圖也只裁切圖樣範圍，不保存其他桌面內容。
    Esc 只在覆蓋層位於前景時送出；快捷鍵只在確認由 ScreenshotApp 註冊時送出，避免按鍵落到其他應用程式。
    每次截圖成功 ScreenshotApp 都會顯示系統通知，執行後通知中心會留下多則通知。
    既有的 settings.json 與故障日誌在結束時還原；剪貼簿的各種格式先盡力備份、結束時放回（無法讀取的格式會在結果中列出）。

.PARAMETER ExePath
    要驗證的 ScreenshotApp.exe；預設為 Release 建置輸出。

.PARAMETER EvidenceDirectory
    存放截圖、量測資料與結果清單的資料夾。

.PARAMETER Cycles
    資源量測的循環次數；每個循環包含一次完成的截圖與一次取消。

.PARAMETER IdleSeconds
    開始前需要的鍵盤與滑鼠閒置秒數；有人正在使用電腦時先等待，避免模擬輸入干擾使用者。

.PARAMETER MaxIdleWaitMinutes
    等待閒置的上限；超過時不做任何變更即結束。
#>
param(
    [string]$ExePath = (Join-Path $PSScriptRoot '..\..\ScreenshotApp\bin\Release\net10.0-windows\ScreenshotApp.exe'),
    [string]$EvidenceDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'ScreenshotApp.Smoke'),
    [int]$Cycles = 30,
    [int]$IdleSeconds = 20,
    [int]$MaxIdleWaitMinutes = 15
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

public static class Phase2Native
{
    public const int WM_CLOSE = 0x0010;
    public const int WM_DISPLAYCHANGE = 0x007E;
    public const int WM_LBUTTONDBLCLK = 0x0203;
    public const int WM_RBUTTONUP = 0x0205;
    // Windows Forms NotifyIcon 的回呼訊息（WM_USER + 1024）。
    public const int WM_TRAYMOUSEMESSAGE = 0x0400 + 1024;
    public const ushort VK_SHIFT = 0x10;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_ESCAPE = 0x1B;
    public const ushort VK_2 = 0x32;
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOPMOST = 0x8;
    public const int WS_EX_TOOLWINDOW = 0x80;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct NOTIFYICONIDENTIFIER { public uint cbSize; public IntPtr hWnd; public uint uID; public Guid guidItem; }

    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

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
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern IntPtr GetDpiAwarenessContextForProcess(IntPtr process);
    [DllImport("user32.dll")] public static extern bool AreDpiAwarenessContextsEqual(IntPtr a, IntPtr b);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr process, uint flags);
    [DllImport("shell32.dll")] static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT rect);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    // 距離最後一次鍵盤或滑鼠輸入的毫秒數。
    public static uint IdleMilliseconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
        GetLastInputInfo(ref info);
        return unchecked((uint)Environment.TickCount - info.dwTime);
    }

    static INPUT KeyInput(ushort vk, bool up)
    {
        var input = new INPUT { type = 1 };
        input.u.ki.wVk = vk;
        input.u.ki.dwFlags = up ? 2u : 0u;
        return input;
    }

    static INPUT MouseInput(uint flags)
    {
        var input = new INPUT { type = 0 };
        input.u.mi.dwFlags = flags;
        return input;
    }

    static uint Send(params INPUT[] inputs) { return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT))); }

    // 依序按下、反序放開，例如 Ctrl+Shift+2。
    public static uint PressKeys(params ushort[] keys)
    {
        var inputs = new List<INPUT>();
        foreach (var key in keys) inputs.Add(KeyInput(key, false));
        for (var i = keys.Length - 1; i >= 0; i--) inputs.Add(KeyInput(keys[i], true));
        return Send(inputs.ToArray());
    }

    // 滑鼠按鍵事件發生在游標目前的位置。
    public static uint LeftDown() { return Send(MouseInput(0x0002)); }
    public static uint LeftUp() { return Send(MouseInput(0x0004)); }
    public static uint LeftClick() { return Send(MouseInput(0x0002), MouseInput(0x0004)); }
    public static uint RightClick() { return Send(MouseInput(0x0008), MouseInput(0x0010)); }

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

    // 各顯示器：left, top, width, height, 縮放百分比（實體像素）。
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

    // 比對 32 位元 BGRA 像素與圖樣；(offsetX, offsetY) 是影像左上角在圖樣中的座標。回傳不一致的像素數。
    public static int CountPatternMismatches(byte[] pixels, int stride, int width, int height, int offsetX, int offsetY, out string first)
    {
        var count = 0;
        first = "";
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * stride + x * 4;
                var actual = (pixels[i + 2] << 16) | (pixels[i + 1] << 8) | pixels[i];
                var expected = PatternColor(offsetX + x, offsetY + y);
                if (actual != expected)
                {
                    if (count == 0) first = string.Format("({0},{1}) 實際 #{2:X6} 預期 #{3:X6}", x, y, actual, expected);
                    count++;
                }
            }
        }
        return count;
    }
}
'@

# 以 Per-Monitor V2 執行本腳本的座標計算與螢幕擷取，取得實體像素。
[void][Phase2Native]::SetThreadDpiAwarenessContext([IntPtr]-4)

$ModControl = 0x2
$ModShift = 0x4
$ModNoRepeat = 0x4000
$ErrorHotkeyAlreadyRegistered = 1409
$OverlayTitle = 'ScreenshotApp 區域選取'
$SettingsTitle = 'ScreenshotApp 設定'
$Sentinel = 'ScreenshotApp 煙霧驗證：剪貼簿不應被取消的截圖改變'
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

function Get-AppProcesses { @(Get-Process -Name 'ScreenshotApp' -ErrorAction SilentlyContinue) }

function Start-App {
    $process = Start-Process -FilePath $ExePath -PassThru
    $ready = Wait-Until {
        $window = [IntPtr]::Zero; $id = [uint32]0
        [Phase2Native]::TryFindTrayIcon([uint32]$process.Id, [ref]$window, [ref]$id)
    } 8000
    return [pscustomobject]@{ Process = $process; TrayReady = [bool]$ready }
}

# 外部嘗試註冊 Ctrl+Shift+2：被占用時回傳錯誤碼，成功時立即解除並回傳 0。
function Test-HotkeyRegistration {
    if ([Phase2Native]::RegisterHotKey([IntPtr]::Zero, 0x5201, $ModControl -bor $ModShift -bor $ModNoRepeat, [Phase2Native]::VK_2)) {
        [void][Phase2Native]::UnregisterHotKey([IntPtr]::Zero, 0x5201)
        return 0
    }
    return [Runtime.InteropServices.Marshal]::GetLastWin32Error()
}

# 只在快捷鍵確實由 ScreenshotApp 持有時送出，否則 Ctrl+Shift+2 會落到前景的其他應用程式。
function Send-Hotkey {
    if ((Test-HotkeyRegistration) -ne $ErrorHotkeyAlreadyRegistered) { throw 'Ctrl+Shift+2 未被 ScreenshotApp 註冊，停止送出快捷鍵。' }
    [void][Phase2Native]::PressKeys([Phase2Native]::VK_CONTROL, [Phase2Native]::VK_SHIFT, [Phase2Native]::VK_2)
}

function Get-Overlays([int]$ProcessId) {
    @([Phase2Native]::TopLevelWindows([uint32]$ProcessId) |
        Where-Object { [Phase2Native]::IsWindowVisible($_) -and [Phase2Native]::TextOf($_) -eq $OverlayTitle })
}

function Wait-Overlays([int]$ProcessId, [int]$Count) {
    Wait-Until { $overlays = @(Get-Overlays $ProcessId); if ($overlays.Count -eq $Count) { , $overlays } } 5000
}

function Wait-NoOverlays([int]$ProcessId) { [bool](Wait-Until { @(Get-Overlays $ProcessId).Count -eq 0 } 5000) }

function Test-OverlayIsForeground([int]$ProcessId) { (Get-Overlays $ProcessId) -contains [Phase2Native]::GetForegroundWindow() }

# Esc 只在覆蓋層位於前景時送出；否則按鍵會落到其他應用程式。回傳是否已送出。
function Send-EscapeToOverlay([int]$ProcessId) {
    if (-not (Wait-Until { Test-OverlayIsForeground $ProcessId } 2000)) { return $false }
    [void][Phase2Native]::PressKeys([Phase2Native]::VK_ESCAPE)
    return $true
}

# 在桌面實體像素座標之間拖曳；$BeforeRelease 在放開前執行（例如擷取選取中的畫面）。
function Invoke-Drag([int]$FromX, [int]$FromY, [int]$ToX, [int]$ToY, [scriptblock]$BeforeRelease) {
    [void][Phase2Native]::SetCursorPos($FromX, $FromY)
    Start-Sleep -Milliseconds 60
    [void][Phase2Native]::LeftDown()
    Start-Sleep -Milliseconds 60
    foreach ($step in 1..4) {
        [void][Phase2Native]::SetCursorPos([int]($FromX + ($ToX - $FromX) * $step / 4), [int]($FromY + ($ToY - $FromY) * $step / 4))
        Start-Sleep -Milliseconds 40
    }
    if ($BeforeRelease) { & $BeforeRelease }
    [void][Phase2Native]::LeftUp()
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

function Get-ScreenBitmap([int]$X, [int]$Y, [int]$Width, [int]$Height) {
    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen($X, $Y, 0, 0, $bitmap.Size) } finally { $graphics.Dispose() }
    return $bitmap
}

# 證據只保存圖樣視窗範圍的畫面，不保存其他桌面內容。
function Save-PatternEvidence([string]$Name) {
    $path = Join-Path $EvidenceDirectory $Name
    $bitmap = Get-ScreenBitmap $Pattern.X $Pattern.Y $Pattern.Width $Pattern.Height
    try { $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png) } finally { $bitmap.Dispose() }
    return [IO.Path]::GetFileName($path)
}

# 剪貼簿可能正被 ScreenshotApp 寫入，讀取失敗時稍後重試。
function Invoke-Clipboard([scriptblock]$Action) {
    foreach ($attempt in 1..20) {
        try { return & $Action }
        catch [System.Runtime.InteropServices.ExternalException] { Start-Sleep -Milliseconds 50 }
    }
    throw '無法存取剪貼簿。'
}

function Set-ClipboardSentinel { Invoke-Clipboard { [System.Windows.Forms.Clipboard]::SetText($Sentinel) } }

function Test-ClipboardIsSentinel {
    Invoke-Clipboard {
        -not [System.Windows.Forms.Clipboard]::ContainsImage() -and [System.Windows.Forms.Clipboard]::ContainsText() -and
        [System.Windows.Forms.Clipboard]::GetText() -eq $Sentinel
    }
}

# 等剪貼簿出現指定尺寸的圖片，並與圖樣比對；(PatternX, PatternY) 為選取範圍左上角在圖樣中的座標。
function Test-ClipboardMatchesPattern([int]$Width, [int]$Height, [int]$PatternX, [int]$PatternY) {
    $image = Wait-Until { Invoke-Clipboard { if ([System.Windows.Forms.Clipboard]::ContainsImage()) { [System.Windows.Forms.Clipboard]::GetImage() } } } 5000
    if (-not $image) { return [pscustomobject]@{ Passed = $false; Detail = '剪貼簿沒有圖片' } }
    try {
        $bitmap = [System.Drawing.Bitmap]::new($image)
        try {
            $pixels = Get-Pixels $bitmap
            $first = ''
            $mismatches = [Phase2Native]::CountPatternMismatches($pixels.Bytes, $pixels.Stride, $pixels.Width, $pixels.Height, $PatternX, $PatternY, [ref]$first)
            $sizeOk = $image.Width -eq $Width -and $image.Height -eq $Height
            return [pscustomobject]@{
                Passed = $sizeOk -and $mismatches -eq 0
                Detail = "剪貼簿 {0}×{1}（預期 {2}×{3}），與圖樣不一致的像素 {4} 個 {5}" -f $image.Width, $image.Height, $Width, $Height, $mismatches, $first
            }
        }
        finally { $bitmap.Dispose() }
    }
    finally { $image.Dispose() }
}

function Send-TrayMessage([int]$ProcessId, [int]$MouseMessage) {
    foreach ($hWnd in [Phase2Native]::HiddenFormsWindows([uint32]$ProcessId)) {
        [void][Phase2Native]::PostMessage($hWnd, [Phase2Native]::WM_TRAYMOUSEMESSAGE, [IntPtr]1, [IntPtr]$MouseMessage)
    }
}

function Find-TrayMenuWindow([int]$ProcessId) {
    foreach ($hWnd in [Phase2Native]::TopLevelWindows([uint32]$ProcessId)) {
        if ([Phase2Native]::IsWindowVisible($hWnd) -and [Phase2Native]::ClassOf($hWnd).StartsWith('WindowsForms10.Window')) { return $hWnd }
    }
    return $null
}

# 開啟系統匣選單並以 UI Automation 執行名稱以 $Prefix 開頭的項目。回傳選單視窗矩形（失敗為 $null）。
function Invoke-TrayMenuItem([int]$ProcessId, [string]$Prefix, [string]$EvidenceName) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::MenuItem)
    foreach ($attempt in 1..3) {
        Send-TrayMessage $ProcessId ([Phase2Native]::WM_RBUTTONUP)
        $menuWindow = Wait-Until { Find-TrayMenuWindow $ProcessId } 3000
        if (-not $menuWindow) { continue }
        $items = @([System.Windows.Automation.AutomationElement]::FromHandle($menuWindow).FindAll(
                [System.Windows.Automation.TreeScope]::Descendants, $condition))
        $item = $items | Where-Object { $_.Current.Name -like "$Prefix*" } | Select-Object -First 1
        if (-not $item) { continue }
        $rect = [Phase2Native+RECT]::new()
        [void][Phase2Native]::GetWindowRect($menuWindow, [ref]$rect)
        if ($EvidenceName) {
            Start-Sleep -Milliseconds 300
            Add-Info '系統匣選單' ("{0}；截圖 {1}" -f (($items | ForEach-Object { $_.Current.Name }) -join '、'), (Save-PatternEvidence $EvidenceName))
        }
        $item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return $rect
    }
    return $null
}

function Find-SettingsWindow([int]$ProcessId) {
    foreach ($hWnd in [Phase2Native]::TopLevelWindows([uint32]$ProcessId)) {
        if ([Phase2Native]::IsWindowVisible($hWnd) -and [Phase2Native]::TextOf($hWnd) -eq $SettingsTitle) { return $hWnd }
    }
    return $null
}

function Stop-AppViaTrayMenu([System.Diagnostics.Process]$Process) {
    $rect = Invoke-TrayMenuItem $Process.Id '結束' $null
    return $rect -and $Process.WaitForExit(8000)
}

# 完成一次截圖：送快捷鍵、等覆蓋層、拖曳、等覆蓋層關閉。回傳是否順利。
# 先在剪貼簿放入文字，之後讀到的圖片一定是這次截圖的結果，而不是上一次留下的。
function Invoke-Capture([int]$ProcessId, [int]$MonitorCount, [int]$FromX, [int]$FromY, [int]$ToX, [int]$ToY) {
    Set-ClipboardSentinel
    Send-Hotkey
    if (-not (Wait-Overlays $ProcessId $MonitorCount)) { return $false }
    Invoke-Drag $FromX $FromY $ToX $ToY $null
    return (Wait-NoOverlays $ProcessId)
}

function Get-Resources([System.Diagnostics.Process]$Process) {
    $Process.Refresh()
    [pscustomobject]@{
        Gdi = [Phase2Native]::GetGuiResources($Process.Handle, 0)
        User = [Phase2Native]::GetGuiResources($Process.Handle, 1)
        Handles = $Process.HandleCount
        PrivateMB = [math]::Round($Process.PrivateMemorySize64 / 1MB, 1)
        WorkingSetMB = [math]::Round($Process.WorkingSet64 / 1MB, 1)
    }
}

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$ExePath = (Resolve-Path $ExePath).Path
if (@(Get-AppProcesses).Count -gt 0) { throw '請先結束正在執行的 ScreenshotApp。' }

# 驗證期間會移動滑鼠與送出按鍵；等到使用者一段時間沒有操作才開始，在此之前不做任何變更。
$idleWatch = [Diagnostics.Stopwatch]::StartNew()
while ([Phase2Native]::IdleMilliseconds() -lt $IdleSeconds * 1000) {
    if ($idleWatch.Elapsed.TotalMinutes -ge $MaxIdleWaitMinutes) {
        throw "等待 $MaxIdleWaitMinutes 分鐘仍有人在操作電腦，未開始驗證，也沒有做任何變更。"
    }
    Start-Sleep -Seconds 1
}
Write-Host ("[INFO] 已閒置 {0} 秒，開始驗證（等待 {1:N0} 秒）" -f $IdleSeconds, $idleWatch.Elapsed.TotalSeconds)

# 測試前狀態：設定檔移開（以預設 Ctrl+Shift+2 啟動）、記錄故障日誌長度、備份剪貼簿文字。
$dataDirectoryExisted = Test-Path $DataDirectory
$logDirectoryExisted = Test-Path $LogDirectory
$settingsBackup = $null
if (Test-Path $SettingsPath) {
    $settingsBackup = Join-Path $EvidenceDirectory 'settings.backup.json'
    Move-Item $SettingsPath $settingsBackup -Force
}
$logExisted = Test-Path $LogPath
$logLengthBefore = if ($logExisted) { (Get-Item $LogPath).Length } else { 0 }

# 盡力備份剪貼簿的每一種格式（文字、圖片、檔案清單等），結束時放回；無法讀取的格式會列出。
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

$monitors = @([Phase2Native]::Monitors())
$primary = $monitors | Where-Object { $_[0] -eq 0 -and $_[1] -eq 0 } | Select-Object -First 1
$patternWidth = [math]::Min(1200, $primary[2] - 200)
$patternHeight = [math]::Min(800, $primary[3] - 200)
$Pattern = [pscustomobject]@{
    X = [int](($primary[2] - $patternWidth) / 2); Y = [int](($primary[3] - $patternHeight) / 2)
    Width = $patternWidth; Height = $patternHeight
}
$patternProcess = $null
$app = $null

try {
    Add-Info '顯示器' (($monitors | ForEach-Object { '({0},{1}) {2}×{3} {4}%' -f $_[0], $_[1], $_[2], $_[3], $_[4] }) -join '；')
    Add-Info '執行檔' ([IO.Path]::GetRelativePath($RepositoryRoot, $ExePath))
    Add-Info '圖樣視窗' ('({0},{1}) {2}×{3}' -f $Pattern.X, $Pattern.Y, $Pattern.Width, $Pattern.Height)
    Add-Info '剪貼簿備份' ("格式 {0} 種{1}" -f $clipboardFormats.Count, $(if ($clipboardSkipped) { '；無法讀取而不會還原：' + ($clipboardSkipped -join '、') } else { '' }))

    # 0. 已知圖樣視窗：先確認螢幕上的像素與圖樣完全一致，後續比對才有意義。
    $patternProcess = Start-Process pwsh -PassThru -WindowStyle Hidden -ArgumentList @(
        '-NoProfile', '-File', (Join-Path $PSScriptRoot 'Show-TestPattern.ps1'),
        '-X', $Pattern.X, '-Y', $Pattern.Y, '-Width', $Pattern.Width, '-Height', $Pattern.Height)
    $patternMismatches = -1
    $patternFirst = ''
    $patternReady = Wait-Until {
        $bitmap = Get-ScreenBitmap $Pattern.X $Pattern.Y $Pattern.Width $Pattern.Height
        try {
            $pixels = Get-Pixels $bitmap
            $first = ''
            $script:patternMismatches = [Phase2Native]::CountPatternMismatches($pixels.Bytes, $pixels.Stride, $pixels.Width, $pixels.Height, 0, 0, [ref]$first)
            $script:patternFirst = $first
            $script:patternMismatches -eq 0
        }
        finally { $bitmap.Dispose() }
    } 15000
    Add-Result '圖樣視窗與螢幕像素一致' ([bool]$patternReady) ('{0}×{1} 像素逐一比對，不一致 {2} 個 {3}' -f $Pattern.Width, $Pattern.Height, $patternMismatches, $patternFirst)
    if (-not $patternReady) { throw '圖樣視窗沒有正確顯示，無法進行像素比對。' }

    # 1. 啟動並確認快捷鍵已註冊
    $app = Start-App
    $appId = $app.Process.Id
    Add-Result '啟動後常駐' ($app.TrayReady -and -not $app.Process.HasExited) "PID $appId"
    Add-Result '快捷鍵已由 ScreenshotApp 註冊' ((Test-HotkeyRegistration) -eq $ErrorHotkeyAlreadyRegistered) 'Ctrl+Shift+2'
    $context = [Phase2Native]::GetDpiAwarenessContextForProcess($app.Process.Handle)
    Add-Result 'DPI 感知為 Per-Monitor V2' ([Phase2Native]::AreDpiAwarenessContextsEqual($context, [IntPtr]-4)) ''

    # 2. 快捷鍵 → 每個顯示器一個覆蓋層，精確貼合、置頂、取得前景
    $foregroundBefore = [Phase2Native]::GetForegroundWindow()
    $foregroundOwner = [uint32]0
    [void][Phase2Native]::GetWindowThreadProcessId($foregroundBefore, [ref]$foregroundOwner)
    Add-Info '按下快捷鍵前的前景' ('{0}（PID {1}，非 ScreenshotApp：{2}）' -f [Phase2Native]::ClassOf($foregroundBefore), $foregroundOwner, ($foregroundOwner -ne $appId))
    Set-ClipboardSentinel
    $watch = [Diagnostics.Stopwatch]::StartNew()
    Send-Hotkey
    $overlays = Wait-Overlays $appId $monitors.Count
    $latency = $watch.ElapsedMilliseconds
    Add-Result '快捷鍵開啟覆蓋層' ([bool]$overlays) ("覆蓋層 {0} 個（顯示器 {1} 個），約 {2} ms（含輪詢間隔）" -f @($overlays).Count, $monitors.Count, $latency)
    if ($overlays) {
        $mismatched = @()
        foreach ($monitor in $monitors) {
            $match = $overlays | Where-Object {
                $rect = [Phase2Native+RECT]::new()
                [void][Phase2Native]::GetWindowRect($_, [ref]$rect)
                $rect.Left -eq $monitor[0] -and $rect.Top -eq $monitor[1] -and ($rect.Right - $rect.Left) -eq $monitor[2] -and ($rect.Bottom - $rect.Top) -eq $monitor[3]
            } | Select-Object -First 1
            if (-not $match) { $mismatched += '({0},{1}) {2}×{3}' -f $monitor[0], $monitor[1], $monitor[2], $monitor[3] }
            elseif ([Phase2Native]::GetDpiForWindow($match) * 100 / 96 -ne $monitor[4]) { $mismatched += "DPI 不符：$($monitor[4])%" }
        }
        Add-Result '覆蓋層精確貼合各顯示器且 DPI 相同' ($mismatched.Count -eq 0) $(if ($mismatched) { $mismatched -join '；' } else { '視窗矩形 = 顯示器實體邊界' })
        $styles = $overlays | ForEach-Object { [Phase2Native]::GetWindowLong($_, [Phase2Native]::GWL_EXSTYLE) }
        Add-Result '覆蓋層置頂且不出現在工作列' (@($styles | Where-Object { ($_ -band 0x88) -ne 0x88 }).Count -eq 0) 'WS_EX_TOPMOST | WS_EX_TOOLWINDOW'
        Add-Result '覆蓋層取得前景' ([bool](Wait-Until { Test-OverlayIsForeground $appId } 2000)) 'Esc 才能取消'
        Start-Sleep -Milliseconds 300
        Add-Info '選取前的覆蓋層' (Save-PatternEvidence 'phase2-overlay-idle.png')

        # 3. 拖曳選取（左上 → 右下）：剪貼簿圖片與圖樣逐像素一致，尺寸包含起點與終點像素
        $from = @(100, 80); $to = @(499, 379)
        Invoke-Drag ($Pattern.X + $from[0]) ($Pattern.Y + $from[1]) ($Pattern.X + $to[0]) ($Pattern.Y + $to[1]) {
            Start-Sleep -Milliseconds 300
            Add-Info '選取中的覆蓋層' (Save-PatternEvidence 'phase2-overlay-selecting.png')
            # 選取範圍內沒有暗化，螢幕上看到的就是覆蓋層畫出的快照；有任何 DPI 偏移或縮放都會與圖樣不一致。
            $bitmap = Get-ScreenBitmap ($Pattern.X + $from[0]) ($Pattern.Y + $from[1]) 400 300
            try {
                $pixels = Get-Pixels $bitmap
                $first = ''
                $mismatches = [Phase2Native]::CountPatternMismatches($pixels.Bytes, $pixels.Stride, 400, 300, $from[0], $from[1], [ref]$first)
                Add-Result '覆蓋層顯示的快照與畫面逐像素一致' ($mismatches -eq 0) "選取範圍 400×300 內不一致的像素 $mismatches 個 $first"
            }
            finally { $bitmap.Dispose() }
        }
        Add-Result '放開滑鼠後關閉全部覆蓋層' (Wait-NoOverlays $appId) ''
        $check = Test-ClipboardMatchesPattern 400 300 $from[0] $from[1]
        Add-Result '選取結果與圖樣逐像素一致（左上→右下）' $check.Passed $check.Detail
    }

    # 4. 反方向拖曳（右下 → 左上）
    Add-Result '反方向拖曳完成截圖' (Invoke-Capture $appId $monitors.Count ($Pattern.X + 899) ($Pattern.Y + 599) ($Pattern.X + 600) ($Pattern.Y + 450)) ''
    $check = Test-ClipboardMatchesPattern 300 150 600 450
    Add-Result '選取結果與圖樣逐像素一致（右下→左上）' $check.Passed $check.Detail

    # 5. Esc 取消：不留覆蓋層、剪貼簿不變
    Set-ClipboardSentinel
    Send-Hotkey
    $opened = Wait-Overlays $appId $monitors.Count
    $sent = $opened -and (Send-EscapeToOverlay $appId)
    Add-Result 'Esc 取消並關閉全部覆蓋層' ($sent -and (Wait-NoOverlays $appId)) $(if ($sent) { '' } else { '覆蓋層不在前景，未送出 Esc' })
    Add-Result 'Esc 取消後剪貼簿不變' (Test-ClipboardIsSentinel) ''

    # 6. 滑鼠右鍵取消
    Send-Hotkey
    $opened = Wait-Overlays $appId $monitors.Count
    if ($opened) {
        [void][Phase2Native]::SetCursorPos($Pattern.X + 300, $Pattern.Y + 300)
        Start-Sleep -Milliseconds 60
        [void][Phase2Native]::RightClick()
    }
    Add-Result '滑鼠右鍵取消並關閉全部覆蓋層' ([bool]$opened -and (Wait-NoOverlays $appId)) ''
    Add-Result '右鍵取消後剪貼簿不變' (Test-ClipboardIsSentinel) ''

    # 7. 在原地點一下（零尺寸）：不產生截圖、維持選取，之後仍可用 Esc 取消
    Send-Hotkey
    $opened = Wait-Overlays $appId $monitors.Count
    if ($opened) {
        [void][Phase2Native]::SetCursorPos($Pattern.X + 400, $Pattern.Y + 400)
        Start-Sleep -Milliseconds 60
        [void][Phase2Native]::LeftClick()
        Start-Sleep -Milliseconds 700
    }
    $stillSelecting = @(Get-Overlays $appId).Count -eq $monitors.Count
    Add-Result '零尺寸點選後維持選取狀態' ([bool]$opened -and $stillSelecting) "覆蓋層 $(@(Get-Overlays $appId).Count) 個"
    $sent = $stillSelecting -and (Send-EscapeToOverlay $appId)
    Add-Result '零尺寸後可用 Esc 取消' ($sent -and (Wait-NoOverlays $appId)) ''
    Add-Result '零尺寸不改變剪貼簿' (Test-ClipboardIsSentinel) ''

    # 8. 選取中再按快捷鍵：不重入，覆蓋層不變
    Send-Hotkey
    $first = Wait-Overlays $appId $monitors.Count
    if ($first) {
        Send-Hotkey
        Start-Sleep -Milliseconds 700
        $second = @(Get-Overlays $appId)
        $same = $second.Count -eq @($first).Count -and @($second | Where-Object { @($first) -notcontains $_ }).Count -eq 0
        Add-Result '選取中再按快捷鍵不重入' $same ("覆蓋層 {0} 個，與先前相同：{1}" -f $second.Count, $same)
        $sent = Send-EscapeToOverlay $appId
        Add-Result '重入測試後可取消' ($sent -and (Wait-NoOverlays $appId)) ''
    }
    else {
        Add-Result '選取中再按快捷鍵不重入' $false '覆蓋層未出現'
    }

    # 9. 自家設定視窗擋在圖樣上：截圖前隱藏、不被截入，結束後恢復
    Send-TrayMessage $appId ([Phase2Native]::WM_LBUTTONDBLCLK)
    $settingsWindow = Wait-Until { Find-SettingsWindow $appId } 8000
    if ($settingsWindow) {
        # 圖樣視窗是置頂視窗，把設定視窗也設為置頂並移到圖樣上，模擬自家視窗遮住要截取的畫面。
        [void][Phase2Native]::SetWindowPos($settingsWindow, [Phase2Native]::HWND_TOPMOST, $Pattern.X + 300, $Pattern.Y + 150, 0, 0, [Phase2Native]::SWP_NOSIZE)
        Start-Sleep -Milliseconds 600
        Add-Info '設定視窗擋在圖樣上' (Save-PatternEvidence 'phase2-settings-over-pattern.png')
        Set-ClipboardSentinel
        Send-Hotkey
        $opened = Wait-Overlays $appId $monitors.Count
        Add-Result '截圖期間設定視窗已隱藏' ([bool]$opened -and -not [Phase2Native]::IsWindowVisible($settingsWindow)) ''
        if ($opened) {
            Invoke-Drag ($Pattern.X + 250) ($Pattern.Y + 100) ($Pattern.X + 1149) ($Pattern.Y + 749) $null
            [void](Wait-NoOverlays $appId)
        }
        $check = Test-ClipboardMatchesPattern 900 650 250 100
        Add-Result '設定視窗沒有被截入' $check.Passed $check.Detail
        Add-Result '截圖後設定視窗恢復顯示' ([bool](Wait-Until { [Phase2Native]::IsWindowVisible($settingsWindow) } 3000)) ''
        [void][Phase2Native]::PostMessage($settingsWindow, [Phase2Native]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
        [void](Wait-Until { -not (Find-SettingsWindow $appId) } 3000)
    }
    else {
        Add-Result '截圖期間設定視窗已隱藏' $false '設定視窗未出現'
    }

    # 10. 系統匣「開始截圖」：選單出現在圖樣上，截圖不包含選單
    [void][Phase2Native]::SetCursorPos($Pattern.X + 700, $Pattern.Y + 300)
    Start-Sleep -Milliseconds 100
    Set-ClipboardSentinel
    $menuRect = Invoke-TrayMenuItem $appId '開始截圖' 'phase2-tray-menu.png'
    $opened = $menuRect -and (Wait-Overlays $appId $monitors.Count)
    Add-Result '系統匣「開始截圖」開啟覆蓋層' ([bool]$opened) $(if ($menuRect) { '選單 ({0},{1})-({2},{3})' -f $menuRect.Left, $menuRect.Top, $menuRect.Right, $menuRect.Bottom } else { '找不到選單項目' })
    if ($opened) {
        Invoke-Drag ($Pattern.X + 650) ($Pattern.Y + 250) ($Pattern.X + 1149) ($Pattern.Y + 649) $null
        [void](Wait-NoOverlays $appId)
        $check = Test-ClipboardMatchesPattern 500 400 650 250
        Add-Result '系統匣選單沒有被截入' $check.Passed $check.Detail
    }

    # 11. 選取期間顯示設定變更（以 WM_DISPLAYCHANGE 模擬）：安全取消、程式仍可再次截圖
    Send-Hotkey
    $opened = Wait-Overlays $appId $monitors.Count
    if ($opened) {
        [void][Phase2Native]::PostMessage(@($opened)[0], [Phase2Native]::WM_DISPLAYCHANGE, [IntPtr]32, [IntPtr]0)
        Add-Result '顯示設定變更時關閉全部覆蓋層' (Wait-NoOverlays $appId) ''
        Add-Result '顯示設定變更後程式仍在執行' (-not $app.Process.HasExited) ''
        Send-Hotkey
        $again = Wait-Overlays $appId $monitors.Count
        $sent = $again -and (Send-EscapeToOverlay $appId)
        Add-Result '顯示設定變更後可再次截圖' ($sent -and (Wait-NoOverlays $appId)) ''
    }
    else {
        Add-Result '顯示設定變更時關閉全部覆蓋層' $false '覆蓋層未出現'
    }

    # 12. 重複循環：每次包含一次完成的截圖與一次取消，量測 GDI／USER 物件、handle 與記憶體
    $samples = [System.Collections.Generic.List[object]]::new()
    $baseline = Get-Resources $app.Process
    $samples.Add([pscustomobject]@{ Cycle = 0; Gdi = $baseline.Gdi; User = $baseline.User; Handles = $baseline.Handles; PrivateMB = $baseline.PrivateMB; WorkingSetMB = $baseline.WorkingSetMB })
    $failures = 0
    foreach ($cycle in 1..$Cycles) {
        $offset = $cycle * 7
        $completed = Invoke-Capture $appId $monitors.Count ($Pattern.X + 50 + $offset) ($Pattern.Y + 40 + $offset) ($Pattern.X + 649 + $offset) ($Pattern.Y + 439 + $offset)
        $copied = $completed -and (Test-ClipboardMatchesPattern 600 400 (50 + $offset) (40 + $offset)).Passed
        Send-Hotkey
        $cancelled = (Wait-Overlays $appId $monitors.Count) -and (Send-EscapeToOverlay $appId) -and (Wait-NoOverlays $appId)
        if (-not ($copied -and $cancelled)) { $failures++ }
        $resources = Get-Resources $app.Process
        $samples.Add([pscustomobject]@{ Cycle = $cycle; Gdi = $resources.Gdi; User = $resources.User; Handles = $resources.Handles; PrivateMB = $resources.PrivateMB; WorkingSetMB = $resources.WorkingSetMB })
    }
    $samples | Export-Csv (Join-Path $EvidenceDirectory 'phase2-resources.csv') -NoTypeInformation -Encoding utf8
    Add-Result "連續 $Cycles 次循環皆成功" ($failures -eq 0) "失敗 $failures 次；每次循環 = 一次完成（逐像素比對）＋一次 Esc 取消"

    $measured = @($samples | Where-Object Cycle -ge 1)
    $window = [math]::Min(10, [math]::Floor($measured.Count / 2))
    $head = @($measured | Select-Object -First $window)
    $tail = @($measured | Select-Object -Last $window)
    $summary = { param($name) '{0}：前 {1} 次 {2}～{3}，後 {1} 次 {4}～{5}' -f $name, $window,
        ($head | Measure-Object $name -Minimum).Minimum, ($head | Measure-Object $name -Maximum).Maximum,
        ($tail | Measure-Object $name -Minimum).Minimum, ($tail | Measure-Object $name -Maximum).Maximum }
    Add-Result 'GDI 物件數沒有成長' (($tail | Measure-Object Gdi -Maximum).Maximum -le ($head | Measure-Object Gdi -Maximum).Maximum) (& $summary 'Gdi')
    Add-Result 'USER 物件數沒有成長' (($tail | Measure-Object User -Maximum).Maximum -le ($head | Measure-Object User -Maximum).Maximum) (& $summary 'User')
    $handleGrowth = ($tail | Measure-Object Handles -Average).Average - ($head | Measure-Object Handles -Average).Average
    Add-Result 'Handle 數沒有持續成長' ($handleGrowth -le 20) ((& $summary 'Handles') + ('；平均差 {0:N1}' -f $handleGrowth))
    $memoryGrowth = ($tail | Measure-Object PrivateMB -Average).Average - ($head | Measure-Object PrivateMB -Average).Average
    Add-Result '私有記憶體沒有持續成長' ($memoryGrowth -le 150) ((& $summary 'PrivateMB') + (' MB；平均差 {0:N1} MB（每次快照約 {1:N0} MB，由垃圾回收釋放）' -f $memoryGrowth, ($monitors | ForEach-Object { $_[2] * $_[3] * 4 / 1MB } | Measure-Object -Sum).Sum))

    # 13. 由系統匣「結束」
    Add-Result '系統匣「結束」後處理程序結束' (Stop-AppViaTrayMenu $app.Process) "ExitCode=$(if ($app.Process.HasExited) { $app.Process.ExitCode } else { '未結束' })"
    Add-Result '結束後快捷鍵已釋放' ((Test-HotkeyRegistration) -eq 0) ''

    # 14. 故障日誌只記錄模擬的顯示設定變更
    $newLog = ''
    if (Test-Path $LogPath) {
        $stream = [IO.File]::Open($LogPath, 'Open', 'Read', 'ReadWrite')
        try {
            [void]$stream.Seek($logLengthBefore, 'Begin')
            $newLog = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8).ReadToEnd()
        }
        finally { $stream.Dispose() }
    }
    $entries = @([regex]::Matches($newLog, '(?m)^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2} (.+?)\r?$') | ForEach-Object { $_.Groups[1].Value })
    Add-Result '故障日誌只有模擬顯示設定變更的記錄' ($entries.Count -eq 1 -and $entries[0] -eq '截圖失敗' -and $newLog -like '*顯示設定已變更*') ("新增記錄 {0} 筆：{1}" -f $entries.Count, ($entries -join '、'))
}
finally {
    if ($app -and -not $app.Process.HasExited) { Get-AppProcesses | Stop-Process -Force -ErrorAction SilentlyContinue }
    if ($patternProcess -and -not $patternProcess.HasExited) {
        [void]$patternProcess.CloseMainWindow()
        if (-not $patternProcess.WaitForExit(3000)) { $patternProcess.Kill() }
    }

    # 還原測試前狀態：設定檔、故障日誌與剪貼簿文字。
    if ($settingsBackup) { Move-Item $settingsBackup $SettingsPath -Force }
    elseif (Test-Path $SettingsPath) { Remove-Item $SettingsPath }
    if ($logExisted) {
        $stream = [IO.File]::Open($LogPath, 'Open', 'Write', 'ReadWrite')
        try { $stream.SetLength($logLengthBefore) } finally { $stream.Dispose() }
    }
    elseif (Test-Path $LogPath) { Remove-Item $LogPath }
    if (-not $logDirectoryExisted -and (Test-Path $LogDirectory) -and -not (Get-ChildItem $LogDirectory -Force)) { Remove-Item $LogDirectory }
    if (-not $dataDirectoryExisted -and (Test-Path $DataDirectory) -and -not (Get-ChildItem $DataDirectory -Force)) { Remove-Item $DataDirectory }
    if ($clipboardFormats.Count -gt 0) { Invoke-Clipboard { [System.Windows.Forms.Clipboard]::SetDataObject($clipboardBackup.Data, $true) } }
    else { Invoke-Clipboard { [System.Windows.Forms.Clipboard]::Clear() } }

    $results | ForEach-Object { "[{0}] {1} — {2}" -f $_.Status, $_.Check, $_.Detail } | Set-Content (Join-Path $EvidenceDirectory 'phase2-results.txt') -Encoding utf8
}

$failed = @($results | Where-Object Status -eq 'FAIL').Count
Write-Host ''
Write-Host ("合計：PASS {0}、FAIL {1}、INFO {2}；證據資料夾 {3}" -f @($results | Where-Object Status -eq 'PASS').Count, $failed, @($results | Where-Object Status -eq 'INFO').Count, $EvidenceDirectory)
exit $(if ($failed -eq 0) { 0 } else { 1 })
