#Requires -Version 7
<#
.SYNOPSIS
    ScreenshotApp 常駐平台煙霧驗證：首次啟動、系統匣、單一執行個體、設定視窗、快捷鍵註冊與占用、DPI 感知、正常結束。

.DESCRIPTION
    會在目前的桌面工作階段實際啟動與結束 ScreenshotApp，期間設定視窗會短暫出現在畫面上。
    執行前請先結束正在執行的 ScreenshotApp。既有的 settings.json 會先備份，結束時還原。
    每項結果輸出為 PASS／FAIL／INFO，並在證據資料夾留下視窗截圖與結果清單。

.PARAMETER ExePath
    要驗證的 ScreenshotApp.exe；預設為 Release 建置輸出。

.PARAMETER EvidenceDirectory
    存放截圖與結果清單的資料夾。
#>
param(
    [string]$ExePath = (Join-Path $PSScriptRoot '..\..\ScreenshotApp\bin\Release\net10.0-windows\ScreenshotApp.exe'),
    [string]$EvidenceDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'ScreenshotApp.Smoke')
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

public static class SmokeNative
{
    public const int WM_CLOSE = 0x0010;
    public const int WM_SETTEXT = 0x000C;
    public const int WM_COMMAND = 0x0111;
    public const int IDOK = 1;
    public const int WM_LBUTTONDBLCLK = 0x0203;
    public const int WM_RBUTTONUP = 0x0205;
    // Windows Forms NotifyIcon 的回呼訊息（WM_USER + 1024）。
    public const int WM_TRAYMOUSEMESSAGE = 0x0400 + 1024;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct NOTIFYICONIDENTIFIER { public uint cbSize; public IntPtr hWnd; public uint uID; public Guid guidItem; }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern IntPtr GetDpiAwarenessContextForProcess(IntPtr hProcess);
    [DllImport("user32.dll")] public static extern bool AreDpiAwarenessContextsEqual(IntPtr a, IntPtr b);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out RECT value, int size);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetDlgItem(IntPtr dialog, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    // 設定對話框中編輯框的文字；編輯框會通知對話框內容已變更。
    public static void SetDialogItemText(IntPtr dialog, int id, string text) { SendMessage(GetDlgItem(dialog, id), WM_SETTEXT, IntPtr.Zero, text); }
    [DllImport("shell32.dll")] public static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT rect);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(long point, uint flags);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);

    // 指定桌面座標所在顯示器的縮放百分比。
    public static int ScalePercentAt(int x, int y)
    {
        uint dpiX, dpiY;
        var monitor = MonitorFromPoint(((long)y << 32) | (uint)x, 2);
        return GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) == 0 ? (int)(dpiX * 100 / 96) : 0;
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

    // 系統匣圖示存在時回傳其所屬視窗與識別碼。
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

    // 優先使用 DWM 的實際外框（不含看不見的縮放邊框），無法取得時改用視窗矩形。
    public static RECT WindowBounds(IntPtr hWnd)
    {
        RECT rect;
        if (DwmGetWindowAttribute(hWnd, 9, out rect, Marshal.SizeOf(typeof(RECT))) != 0 || rect.Right <= rect.Left)
            GetWindowRect(hWnd, out rect);
        return rect;
    }
}
'@

# 以 Per-Monitor V2 執行本腳本的視窗與截圖座標計算，取得實體像素。
[void][SmokeNative]::SetThreadDpiAwarenessContext([IntPtr]-4)

$ModControl = 0x2
$ModShift = 0x4
$ModNoRepeat = 0x4000
$VirtualKey2 = 0x32
$ErrorHotkeyAlreadyRegistered = 1409
$SettingsTitle = 'ScreenshotApp 設定'
$DataDirectory = Join-Path $env:LOCALAPPDATA 'ScreenshotApp'
$SettingsPath = Join-Path $DataDirectory 'settings.json'
$LogPath = Join-Path $DataDirectory 'logs\error.log'

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
        Start-Sleep -Milliseconds 100
    }
    return $null
}

# 以外部程序嘗試註冊快捷鍵（預設 Ctrl+Shift+2）：被占用時回傳錯誤碼，成功時立即解除並回傳 0。
function Test-HotkeyRegistration([int]$Modifiers = ($ModControl -bor $ModShift), [int]$VirtualKey = $VirtualKey2) {
    if ([SmokeNative]::RegisterHotKey([IntPtr]::Zero, 0x5101, $Modifiers -bor $ModNoRepeat, $VirtualKey)) {
        [void][SmokeNative]::UnregisterHotKey([IntPtr]::Zero, 0x5101)
        return 0
    }
    return [Runtime.InteropServices.Marshal]::GetLastWin32Error()
}

function Start-App {
    $process = Start-Process -FilePath $ExePath -PassThru
    $ready = Wait-Until {
        $window = [IntPtr]::Zero; $id = [uint32]0
        [SmokeNative]::TryFindTrayIcon([uint32]$process.Id, [ref]$window, [ref]$id)
    } 8000
    return [pscustomobject]@{ Process = $process; TrayReady = [bool]$ready }
}

function Get-AppProcesses { @(Get-Process -Name 'ScreenshotApp' -ErrorAction SilentlyContinue) }

function Find-SettingsWindow([int]$ProcessId) {
    foreach ($hWnd in [SmokeNative]::TopLevelWindows([uint32]$ProcessId)) {
        if ([SmokeNative]::IsWindowVisible($hWnd) -and [SmokeNative]::TextOf($hWnd) -eq $SettingsTitle) { return $hWnd }
    }
    return $null
}

function Get-VisibleWindowTitles([int]$ProcessId) {
    @([SmokeNative]::TopLevelWindows([uint32]$ProcessId) |
        Where-Object { [SmokeNative]::IsWindowVisible($_) } |
        ForEach-Object { '{0} ({1})' -f [SmokeNative]::TextOf($_), [SmokeNative]::ClassOf($_) })
}

function Get-TextElements([IntPtr]$WindowHandle) {
    $element = [System.Windows.Automation.AutomationElement]::FromHandle($WindowHandle)
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
    @($element.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition) | ForEach-Object { $_.Current.Name })
}

# 預設請視窗把自身內容繪製到點陣圖（不受其他視窗遮擋影響），再裁掉看不見的縮放邊框。
# Windows Forms 下拉選單不支援這種繪製，改用 -FromScreen 直接擷取螢幕（選單位於最上層，不會被遮擋）。
function Save-WindowImage([IntPtr]$WindowHandle, [string]$Name, [switch]$FromScreen) {
    # 等 Windows 的視窗開啟動畫結束，避免截到半透明的過渡畫面。
    Start-Sleep -Milliseconds 600
    $full = [SmokeNative+RECT]::new()
    [void][SmokeNative]::GetWindowRect($WindowHandle, [ref]$full)
    $visible = [SmokeNative]::WindowBounds($WindowHandle)
    $path = Join-Path $EvidenceDirectory $Name
    $bitmap = [System.Drawing.Bitmap]::new($full.Right - $full.Left, $full.Bottom - $full.Top)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        if ($FromScreen) {
            $graphics.CopyFromScreen($full.Left, $full.Top, 0, 0, $bitmap.Size)
        }
        else {
            $hdc = $graphics.GetHdc()
            [void][SmokeNative]::PrintWindow($WindowHandle, $hdc, 2)
            $graphics.ReleaseHdc($hdc)
        }
        $graphics.Dispose()
        $crop = [System.Drawing.Rectangle]::new($visible.Left - $full.Left, $visible.Top - $full.Top, $visible.Right - $visible.Left, $visible.Bottom - $visible.Top)
        $cropped = $bitmap.Clone($crop, $bitmap.PixelFormat)
        try { $cropped.Save($path, [System.Drawing.Imaging.ImageFormat]::Png) } finally { $cropped.Dispose() }
    }
    finally { $bitmap.Dispose() }
    return $path
}

function Send-TrayMessage([int]$ProcessId, [int]$MouseMessage) {
    foreach ($hWnd in [SmokeNative]::HiddenFormsWindows([uint32]$ProcessId)) {
        [void][SmokeNative]::PostMessage($hWnd, [SmokeNative]::WM_TRAYMOUSEMESSAGE, [IntPtr]1, [IntPtr]$MouseMessage)
    }
}

function Find-TrayMenuWindow([int]$ProcessId) {
    foreach ($hWnd in [SmokeNative]::TopLevelWindows([uint32]$ProcessId)) {
        if ([SmokeNative]::IsWindowVisible($hWnd) -and [SmokeNative]::ClassOf($hWnd).StartsWith('WindowsForms10.Window')) { return $hWnd }
    }
    return $null
}

# 以系統匣右鍵選單的「結束」關閉程式，走與使用者相同的程式路徑。
# 先以 Win32 找到選單視窗，再只在該視窗內以 UI Automation 尋找項目；從桌面根節點搜尋整棵樹可能非常慢。
function Stop-AppViaTrayMenu([System.Diagnostics.Process]$Process, [string]$EvidenceName) {
    $menuItemCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::MenuItem)
    foreach ($attempt in 1..3) {
        Send-TrayMessage $Process.Id ([SmokeNative]::WM_RBUTTONUP)
        $menuWindow = Wait-Until { Find-TrayMenuWindow $Process.Id } 3000
        if (-not $menuWindow) { continue }

        $items = @([System.Windows.Automation.AutomationElement]::FromHandle($menuWindow).FindAll(
                [System.Windows.Automation.TreeScope]::Descendants, $menuItemCondition))
        $exitItem = $items | Where-Object { $_.Current.Name -like '結束*' } | Select-Object -First 1
        if (-not $exitItem) { continue }

        if ($EvidenceName) {
            $menuImage = Save-WindowImage $menuWindow $EvidenceName -FromScreen
            Add-Info '系統匣選單項目' ("{0}；截圖 {1}" -f (($items | ForEach-Object { $_.Current.Name }) -join '、'), $menuImage)
        }

        $exitItem.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return $Process.WaitForExit(8000)
    }
    return $false
}

function Find-ById($Root, [string]$AutomationId) {
    $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId))
}

function Get-ElementText($Root, [string]$AutomationId) { (Find-ById $Root $AutomationId).Current.Name }

function Invoke-Element($Root, [string]$AutomationId) {
    (Find-ById $Root $AutomationId).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500
}

# 只有在設定視窗的快捷鍵欄位確實取得鍵盤焦點時才送出按鍵，避免按鍵落到其他應用程式；回傳欄位顯示的組合。
function Send-KeysToHotkeyBox($Root, [string]$Keys) {
    $box = Find-ById $Root 'HotkeyBox'
    $focused = Wait-Until {
        try { $box.SetFocus() } catch [InvalidOperationException] { }
        $current = [System.Windows.Automation.AutomationElement]::FocusedElement
        $current.Current.AutomationId -eq 'HotkeyBox' -and $current.Current.ProcessId -eq $Root.Current.ProcessId
    } 3000
    if (-not $focused) {
        return '（未取得焦點，未送出按鍵）'
    }

    [System.Windows.Forms.SendKeys]::SendWait($Keys)
    Start-Sleep -Milliseconds 300
    return $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
}

function Get-RunValue {
    (Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'ScreenshotApp' -ErrorAction SilentlyContinue)?.ScreenshotApp
}

function Find-ColorDialog([int]$ProcessId) {
    foreach ($hWnd in [SmokeNative]::TopLevelWindows([uint32]$ProcessId)) {
        if ([SmokeNative]::IsWindowVisible($hWnd) -and [SmokeNative]::ClassOf($hWnd) -eq '#32770') { return $hWnd }
    }
    return $null
}

function Get-SelectedColorName($Root) {
    $selection = (Find-ById $Root 'ColorList').GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()
    if ($selection.Count -gt 0) { $selection[0].Current.Name }
}

function Select-ColorByName($Root, [string]$Name) {
    $item = (Find-ById $Root 'ColorList').FindFirst([System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name))
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 200
}

# 等設定視窗出現且內容已能由 UI Automation 讀取：視窗變為可見與內容完成配置之間有短暫延遲。
function Wait-SettingsWindow([int]$ProcessId) {
    Wait-Until {
        $hWnd = Find-SettingsWindow $ProcessId
        if ($hWnd) {
            $status = Find-ById ([System.Windows.Automation.AutomationElement]::FromHandle($hWnd)) 'HotkeyStatusText'
            if ($status -and $status.Current.Name) { return $hWnd }
        }
        $null
    } 8000
}

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$ExePath = (Resolve-Path $ExePath).Path
if (@(Get-AppProcesses).Count -gt 0) { throw '請先結束正在執行的 ScreenshotApp。' }

$dataDirectoryExisted = Test-Path $DataDirectory
$settingsBackup = $null
if (Test-Path $SettingsPath) {
    $settingsBackup = Join-Path $EvidenceDirectory 'settings.backup.json'
    Move-Item $SettingsPath $settingsBackup -Force
}
$logLengthBefore = if (Test-Path $LogPath) { (Get-Item $LogPath).Length } else { 0 }

try {
    $screens = [System.Windows.Forms.Screen]::AllScreens | ForEach-Object { '{0} {1}x{2} @({3},{4}) {5}%{6}' -f $_.DeviceName, $_.Bounds.Width, $_.Bounds.Height, $_.Bounds.X, $_.Bounds.Y, [SmokeNative]::ScalePercentAt($_.Bounds.X, $_.Bounds.Y), $(if ($_.Primary) { ' 主螢幕' } else { '' }) }
    Add-Info '顯示器' ($screens -join '；')
    Add-Info '執行檔' ([IO.Path]::GetRelativePath((Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path, $ExePath))

    # 1. 首次啟動只進系統匣
    $app = Start-App
    $pid1 = $app.Process.Id
    Add-Result '啟動後常駐' (-not $app.Process.HasExited) "PID $pid1"
    Add-Result '系統匣圖示存在' $app.TrayReady 'Shell_NotifyIconGetRect 找到圖示'
    $visible = @(Get-VisibleWindowTitles $pid1)
    Add-Result '啟動後無可見視窗' ($visible.Count -eq 0) ("可見視窗 {0} 個 {1}" -f $visible.Count, ($visible -join ', '))
    $settingsCreated = Wait-Until { Test-Path $SettingsPath } 3000
    $hotkeySetting = if ($settingsCreated) { (Get-Content $SettingsPath -Raw | ConvertFrom-Json).Hotkey } else { $null }
    Add-Result '首次啟動建立 settings.json' ([bool]$settingsCreated -and $hotkeySetting -eq 'Ctrl+Shift+2') "Hotkey=$hotkeySetting"

    $dpiContext = [SmokeNative]::GetDpiAwarenessContextForProcess($app.Process.Handle)
    Add-Result 'DPI 感知為 Per-Monitor V2' ([SmokeNative]::AreDpiAwarenessContextsEqual($dpiContext, [IntPtr]-4)) "context=0x$('{0:X}' -f $dpiContext.ToInt64())"

    $hotkeyError = Test-HotkeyRegistration
    Add-Result '執行中占用 Ctrl+Shift+2' ($hotkeyError -eq $ErrorHotkeyAlreadyRegistered) "外部 RegisterHotKey 錯誤碼 $hotkeyError"

    # 2. 再次啟動：不產生第二個執行個體，既有執行個體顯示設定視窗
    $second = Start-Process -FilePath $ExePath -PassThru
    $secondExited = $second.WaitForExit(8000)
    Add-Result '第二個執行個體自動結束' ($secondExited -and $second.ExitCode -eq 0) "ExitCode=$(if ($secondExited) { $second.ExitCode } else { '未結束' })"
    Add-Result '只有一個處理程序' (@(Get-AppProcesses).Count -eq 1) "處理程序數 $(@(Get-AppProcesses).Count)"
    $settingsWindow = Wait-SettingsWindow $pid1
    Add-Result '再次啟動顯示設定視窗' ([bool]$settingsWindow) $SettingsTitle
    if ($settingsWindow) {
        Start-Sleep -Milliseconds 300
        $isForeground = [SmokeNative]::GetForegroundWindow() -eq $settingsWindow
        Add-Info '設定視窗位於前景' "$isForeground（由背景腳本啟動時 Windows 可能不給予前景權限，需以實機雙擊執行檔確認）"
        $texts = @(Get-TextElements $settingsWindow)
        Add-Result '設定視窗顯示快捷鍵已啟用' ([bool]($texts -contains '目前已啟用：Ctrl+Shift+2')) (($texts | Select-Object -First 3) -join ' | ')
        Add-Info '設定視窗截圖' (Save-WindowImage $settingsWindow 'phase1-settings.png')

        [void][SmokeNative]::PostMessage($settingsWindow, [SmokeNative]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
        $closed = Wait-Until { -not (Find-SettingsWindow $pid1) } 3000
        Add-Result '關閉設定視窗後仍常駐' ([bool]$closed -and -not $app.Process.HasExited) '設定視窗已關閉，處理程序仍在'
    }

    # 3. 雙擊系統匣圖示開啟設定
    Send-TrayMessage $pid1 ([SmokeNative]::WM_LBUTTONDBLCLK)
    $doubleClickWindow = Wait-SettingsWindow $pid1
    Add-Result '雙擊系統匣圖示開啟設定' ([bool]$doubleClickWindow) $SettingsTitle
    if ($doubleClickWindow) {
        [void][SmokeNative]::PostMessage($doubleClickWindow, [SmokeNative]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
        [void](Wait-Until { -not (Find-SettingsWindow $pid1) } 3000)
    }

    # 4. 由系統匣「結束」：處理程序結束並釋放快捷鍵
    $exited = Stop-AppViaTrayMenu $app.Process 'phase1-tray-menu.png'
    Add-Result '系統匣「結束」後處理程序結束' ($exited -and $app.Process.ExitCode -eq 0) "ExitCode=$(if ($exited) { $app.Process.ExitCode } else { '未結束' })"
    $hotkeyAfterExit = Test-HotkeyRegistration
    Add-Result '結束後快捷鍵已釋放' ($hotkeyAfterExit -eq 0) "外部 RegisterHotKey 錯誤碼 $hotkeyAfterExit"

    # 5. 退出後重新啟動，快捷鍵可再註冊
    $restart = Start-App
    $restartHotkey = Test-HotkeyRegistration
    Add-Result '重新啟動後快捷鍵再次註冊' ($restart.TrayReady -and $restartHotkey -eq $ErrorHotkeyAlreadyRegistered) "系統匣就緒 $($restart.TrayReady)；外部 RegisterHotKey 錯誤碼 $restartHotkey"
    $restartExited = Stop-AppViaTrayMenu $restart.Process $null
    Add-Result '重新啟動後可正常結束' $restartExited "ExitCode=$(if ($restartExited) { $restart.Process.ExitCode } else { '未結束' })"

    # 6. 快捷鍵已被其他程式占用：程式仍常駐並顯示原因
    $held = [SmokeNative]::RegisterHotKey([IntPtr]::Zero, 0x5102, $ModControl -bor $ModShift -bor $ModNoRepeat, $VirtualKey2)
    try {
        Add-Result '測試腳本預先占用 Ctrl+Shift+2' $held "RegisterHotKey 傳回 $held"
        $conflict = Start-App
        Add-Result '快捷鍵被占用時仍常駐' ($conflict.TrayReady -and -not $conflict.Process.HasExited) "PID $($conflict.Process.Id)"
        $null = Start-Process -FilePath $ExePath -PassThru
        $conflictWindow = Wait-SettingsWindow $conflict.Process.Id
        if ($conflictWindow) {
            $texts = @(Get-TextElements $conflictWindow)
            $status = $texts | Where-Object { $_ -like '目前未啟用*' } | Select-Object -First 1
            Add-Result '設定視窗顯示占用原因' ([bool]($status -and $status -like '*已被其他程式或 Windows 使用*')) "$status"
            Add-Info '占用情境設定視窗截圖' (Save-WindowImage $conflictWindow 'phase1-settings-hotkey-in-use.png')
        }
        else {
            Add-Result '設定視窗顯示占用原因' $false '設定視窗未出現'
        }
        Add-Info '系統匣通知' '占用時會顯示系統通知；通知可能被專注模式抑制，需由使用者目視確認'
        $conflictExited = Stop-AppViaTrayMenu $conflict.Process $null
        Add-Result '占用情境下可正常結束' $conflictExited ''
    }
    finally {
        if ($held) { [void][SmokeNative]::UnregisterHotKey([IntPtr]::Zero, 0x5102) }
    }

    # 7. 在設定視窗變更快捷鍵與開機啟動：成功立即生效、衝突保留原設定、無效組合不接受
    $VirtualKeyF11 = 0x7A
    $VirtualKeyF12 = 0x7B
    $ModAll = 0x1 -bor $ModControl -bor $ModShift
    $ui = Start-App
    $null = Start-Process -FilePath $ExePath -PassThru
    $uiWindow = Wait-SettingsWindow $ui.Process.Id
    if (-not $uiWindow) {
        Add-Result '設定視窗操作' $false '設定視窗未出現'
    }
    else {
        $root = [System.Windows.Automation.AutomationElement]::FromHandle($uiWindow)

        $shown = Send-KeysToHotkeyBox $root '+a'
        $status = Get-ElementText $root 'HotkeyStatusText'
        Add-Result '拒絕缺少修飾鍵的組合' ($shown -eq 'Ctrl+Shift+2' -and $status -like '無法使用 Shift+A*') "欄位 $shown；$status"

        $shown = Send-KeysToHotkeyBox $root '^%+{F12}'
        Invoke-Element $root 'ApplyButton'
        $result = Get-ElementText $root 'ResultText'
        $newHeld = Test-HotkeyRegistration $ModAll $VirtualKeyF12
        $oldReleased = Test-HotkeyRegistration
        $saved = (Get-Content $SettingsPath -Raw | ConvertFrom-Json).Hotkey
        Add-Result '變更快捷鍵立即生效' ($shown -eq 'Ctrl+Alt+Shift+F12' -and $newHeld -eq $ErrorHotkeyAlreadyRegistered -and $oldReleased -eq 0 -and $saved -eq 'Ctrl+Alt+Shift+F12') "欄位 $shown；新組合錯誤碼 $newHeld；舊組合錯誤碼 $oldReleased；settings.json $saved；$result"

        $shown = Send-KeysToHotkeyBox $root '^%+{F11}'
        $blocker = [SmokeNative]::RegisterHotKey([IntPtr]::Zero, 0x5103, $ModAll -bor $ModNoRepeat, $VirtualKeyF11)
        try {
            Invoke-Element $root 'ApplyButton'
            $result = Get-ElementText $root 'ResultText'
            $stillHeld = Test-HotkeyRegistration $ModAll $VirtualKeyF12
            Add-Result '新組合衝突時保留原設定並提示' ($blocker -and $shown -eq 'Ctrl+Alt+Shift+F11' -and $stillHeld -eq $ErrorHotkeyAlreadyRegistered -and $result -like '*無法使用 Ctrl+Alt+Shift+F11*仍保留原設定 Ctrl+Alt+Shift+F12*') "原組合錯誤碼 $stillHeld；$result"
            Add-Info '衝突提示截圖' (Save-WindowImage $uiWindow 'phase1-settings-rebind-conflict.png')
        }
        finally {
            if ($blocker) { [void][SmokeNative]::UnregisterHotKey([IntPtr]::Zero, 0x5103) }
        }

        $toggle = (Find-ById $root 'StartWithWindowsBox').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
        $toggle.Toggle()
        Invoke-Element $root 'ApplyButton'
        $runValue = Get-RunValue
        Add-Result '啟用登入時啟動寫入 Run 登錄值' ($runValue -eq ('"{0}"' -f $ExePath)) "$runValue"
        $toggle.Toggle()
        Invoke-Element $root 'ApplyButton'
        Add-Result '停用登入時啟動移除 Run 登錄值' ($null -eq (Get-RunValue)) '已移除'

        # 自訂顏色：Windows 色彩對話框輸入 RGB 後確定，成為預設顏色；之後改回紅色。
        Invoke-Element $root 'CustomColorButton'
        $dialog = Wait-Until { Find-ColorDialog $ui.Process.Id } 5000
        Add-Result '開啟自訂顏色對話框' ([bool]$dialog) 'Windows 色彩對話框（#32770）'
        if ($dialog) {
            # Windows 色彩對話框的紅、綠、藍欄位識別碼為 706～708。受控 UI Automation 用戶端沒有 Win32 控制項代理，
            # 這些欄位只呈現為無操作模式的窗格，因此直接以視窗訊息填值並按下確定。
            foreach ($field in @(@(706, '18'), @(707, '171'), @(708, '239'))) {
                [SmokeNative]::SetDialogItemText($dialog, $field[0], $field[1])
            }
            [void][SmokeNative]::PostMessage($dialog, [SmokeNative]::WM_COMMAND, [IntPtr][SmokeNative]::IDOK, [IntPtr]::Zero)
            $dialogClosed = Wait-Until { -not (Find-ColorDialog $ui.Process.Id) } 3000
            $selectedColor = Get-SelectedColorName $root
            Invoke-Element $root 'ApplyButton'
            $savedColor = (Get-Content $SettingsPath -Raw | ConvertFrom-Json).DefaultColor
            Add-Result '自訂顏色成為預設顏色' ([bool]$dialogClosed -and $selectedColor -eq '自訂 #12ABEF' -and $savedColor -eq '#12ABEF') "選取 $selectedColor；settings.json $savedColor"
            Add-Info '自訂顏色截圖' (Save-WindowImage $uiWindow 'phase1-settings-custom-color.png')

            Select-ColorByName $root '紅色'
            Invoke-Element $root 'ApplyButton'
            $savedColor = (Get-Content $SettingsPath -Raw | ConvertFrom-Json).DefaultColor
            Add-Result '選回常用色紅色' ($savedColor -eq '#FF0000') "settings.json $savedColor"
        }

        $shown = Send-KeysToHotkeyBox $root '^+2'
        Invoke-Element $root 'OkButton'
        $closed = Wait-Until { -not (Find-SettingsWindow $ui.Process.Id) } 3000
        $restored = Test-HotkeyRegistration
        Add-Result '改回預設快捷鍵並以確定關閉' ([bool]$closed -and $restored -eq $ErrorHotkeyAlreadyRegistered) "欄位 $shown；Ctrl+Shift+2 錯誤碼 $restored"
    }
    Add-Result '設定操作後可正常結束' (Stop-AppViaTrayMenu $ui.Process $null) ''

    # 8. 故障日誌只在有錯誤時寫入
    $logLengthAfter = if (Test-Path $LogPath) { (Get-Item $LogPath).Length } else { 0 }
    Add-Result '過程中沒有新的錯誤記錄' ($logLengthAfter -eq $logLengthBefore) "error.log 長度 $logLengthBefore → $logLengthAfter"
}
finally {
    Get-AppProcesses | Stop-Process -Force -ErrorAction SilentlyContinue

    # 還原測試前狀態：只移除指向本次執行檔的 Run 值；原本沒有設定檔時刪除本次產生的設定檔。
    if ((Get-RunValue) -eq ('"{0}"' -f $ExePath)) {
        Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'ScreenshotApp'
    }
    if ($settingsBackup) { Move-Item $settingsBackup $SettingsPath -Force }
    elseif (Test-Path $SettingsPath) { Remove-Item $SettingsPath }
    if (-not $dataDirectoryExisted -and (Test-Path $DataDirectory) -and -not (Get-ChildItem $DataDirectory -Force)) {
        Remove-Item $DataDirectory
    }
    $results | ForEach-Object { "[{0}] {1} — {2}" -f $_.Status, $_.Check, $_.Detail } | Set-Content (Join-Path $EvidenceDirectory 'phase1-results.txt') -Encoding utf8
}

$failed = @($results | Where-Object Status -eq 'FAIL').Count
Write-Host ''
Write-Host ("合計：PASS {0}、FAIL {1}、INFO {2}；證據資料夾 {3}" -f @($results | Where-Object Status -eq 'PASS').Count, $failed, @($results | Where-Object Status -eq 'INFO').Count, $EvidenceDirectory)
exit $(if ($failed -eq 0) { 0 } else { 1 })
