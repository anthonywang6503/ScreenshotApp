#Requires -Version 7
<#
.SYNOPSIS
    對使用者指定的測試視窗比較兩種向下滾輪方式；只記錄雜湊變化與穩定時間，不保存畫面。
.DESCRIPTION
    目標必須是可捲動測試文件，且已在前景。只在閒置後執行，任何使用者介入或前景變更都停止。
    不啟動或關閉目標應用程式；若已有 ScreenshotApp，僅記錄尚未驗證矩陣。
#>
param(
    [long]$WindowHandle = 0,
    [ValidateSet('Edge', 'Chrome', 'Word', 'VS Code', 'Explorer', 'Pattern')][string]$ApplicationName = 'Pattern',
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot '../../docs/evidence/phase-13'),
    [ValidateRange(90, 3600)][int]$IdleSeconds = 90,
    [ValidateRange(1, 60)][int]$MaxIdleWaitMinutes = 2
)

. (Join-Path $PSScriptRoot 'Invoke-Phase4Smoke.ps1') -LibraryOnly -EvidenceDirectory $EvidenceDirectory `
    -IdleSeconds $IdleSeconds -MaxIdleWaitMinutes $MaxIdleWaitMinutes

[IO.Directory]::CreateDirectory($EvidenceDirectory) | Out-Null
$records = [Collections.Generic.List[object]]::new()
function Write-Probe {
    $name = if ($WindowHandle -eq 0) { 'wheel-probe-matrix.csv' } else { 'wheel-probe-' + $ApplicationName.Replace(' ', '-') + '.csv' }
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory $name), (($records | ConvertTo-Csv -NoTypeInformation) -join "`n") + "`n")
}

if (@(Get-AppProcesses).Count -gt 0 -or $WindowHandle -eq 0) {
    $reason = if (@(Get-AppProcesses).Count -gt 0) { '既有 ScreenshotApp 執行中，未操作桌面。' } else { '尚未指定在前景的測試視窗。' }
    foreach ($name in @('Edge', 'Chrome', 'Word', 'VS Code', 'Explorer', 'Pattern')) {
        foreach ($method in @('SendInput', 'PostMessage')) {
            $records.Add([pscustomobject]@{ Application = $name; Version = ''; Method = $method; Status = 'NOT_RUN'; Changed = ''; StableMilliseconds = ''; Detail = $reason })
        }
    }
    Write-Probe
    Write-Host "尚未驗證：$reason"
    exit 2
}

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class ScrollProbeNative
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint data, flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort key, scan; public uint flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Explicit)] struct UNION { [FieldOffset(0)] public MOUSEINPUT mouse; [FieldOffset(0)] public KEYBDINPUT key; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public UNION union; }
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint count, INPUT[] input, int size);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    public static bool Wheel() {
        var input = new INPUT { union = new UNION { mouse = new MOUSEINPUT { flags = 0x0800, data = unchecked((uint)-120) } } };
        return SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()) == 1;
    }
    public static bool Post(int x, int y) {
        if (x < short.MinValue || x > short.MaxValue || y < short.MinValue || y > short.MaxValue) return false;
        return PostMessage(WindowFromPoint(new POINT { X=x, Y=y }), 0x020A, (IntPtr)(-120 << 16), (IntPtr)((y << 16) | (x & 0xffff)));
    }
}
'@

$window = [IntPtr]$WindowHandle
$owner = [uint32]0
[void][Phase4Native]::GetWindowThreadProcessId($window, [ref]$owner)
$process = Get-Process -Id $owner -ErrorAction Stop
$version = $process.MainModule.FileVersionInfo.FileVersion
$rect = Get-WindowRect $window
$centerX = $rect.X + [int]($rect.Width / 2)
$centerY = $rect.Y + [int]($rect.Height / 2)
if ($rect.Width -lt 200 -or $rect.Height -lt 200) { throw '測試視窗太小。' }
$watch = [Diagnostics.Stopwatch]::StartNew()
while ([Phase4Native]::IdleMilliseconds() -lt $IdleSeconds * 1000) {
    if ($watch.Elapsed.TotalMinutes -ge $MaxIdleWaitMinutes) { throw '等待閒置逾時，未送出輸入。' }
    Start-Sleep -Seconds 1
}
function Assert-ProbeTarget {
    Assert-NoUserInput
    if ([Phase4Native]::GetForegroundWindow() -ne $window -or [Phase4Native]::RootWindowAt($centerX, $centerY) -ne $window) { Stop-Simulation '目標已失去前景或被遮住' }
    $currentOwner = [uint32]0
    [void][Phase4Native]::GetWindowThreadProcessId($window, [ref]$currentOwner)
    if ($currentOwner -ne $owner -or $process.HasExited) { Stop-Simulation '目標程序已改變' }
}
function Sample-Hash {
    $pixels = Get-ScreenPixels ($centerX - 80) ($centerY - 80) 160 160
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($pixels.Bytes))
}
$originalCursor = [ScrollProbeNative+POINT]::new()
[void][ScrollProbeNative]::GetCursorPos([ref]$originalCursor)
try {
    foreach ($method in @('SendInput', 'PostMessage')) {
        Assert-ProbeTarget
        [Phase4Native]::MoveCursor($centerX, $centerY)
        Start-Sleep -Milliseconds 100
        Assert-ProbeTarget
        $before = Sample-Hash
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $sent = if ($method -eq 'SendInput') { [ScrollProbeNative]::Wheel() } else { [ScrollProbeNative]::Post($centerX, $centerY) }
        $changed = $false; $stable = 0; $last = $before
        while ($timer.ElapsedMilliseconds -lt 5000) {
            Start-Sleep -Milliseconds 150
            Assert-ProbeTarget
            $hash = Sample-Hash
            if ($hash -ne $before) { $changed = $true }
            $stable = if ($hash -eq $last) { $stable + 1 } else { 1 }
            $last = $hash
            if ($changed -and $stable -ge 3) { break }
        }
        $records.Add([pscustomobject]@{ Application = $ApplicationName; Version = $version; Method = $method;
            Status = 'OBSERVED'; Changed = $changed; StableMilliseconds = $(if ($changed -and $stable -ge 3) { $timer.ElapsedMilliseconds } else { '' });
            Detail = "送出成功=$sent；取樣 160×160；每 150ms，三次相同才穩定；無變化也可能已到底。" })
    }
}
finally {
    if (-not [Phase4Native]::UserInputSinceInjection(300) -and [Phase4Native]::GetForegroundWindow() -eq $window) {
        [Phase4Native]::MoveCursor($originalCursor.X, $originalCursor.Y)
    }
    Write-Probe
}
