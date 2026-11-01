#Requires -Version 7
<#
.SYNOPSIS
    在已知圖樣上驗證馬賽克、模糊、復原、PNG 與剪貼簿一致性；只在閒置時操作桌面。
.DESCRIPTION
    必須先結束既有程式。使用既有煙霧工具的輸入守衛、PrintWindow 與像素比對。
    設定與日誌以記憶體備份；剪貼簿若被其他程式更新就保留新內容。啟動登錄值存在時停止，避免改變登入啟動設定。
    執行期間不要啟動其他指令，以免終端機取得前景。未完成的實機項目不算通過。
#>
param(
    [string]$ExePath = (Join-Path $PSScriptRoot '../../ScreenshotApp/bin/Release/net10.0-windows/ScreenshotApp.exe'),
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot '../../docs/evidence/phase-10'),
    [ValidateRange(90, 3600)][int]$IdleSeconds = 90,
    [ValidateRange(1, 60)][int]$MaxIdleWaitMinutes = 2
)

. (Join-Path $PSScriptRoot 'Invoke-Phase4Smoke.ps1') -LibraryOnly -ExePath $ExePath `
    -EvidenceDirectory $EvidenceDirectory -IdleSeconds $IdleSeconds -MaxIdleWaitMinutes $MaxIdleWaitMinutes

function Write-Results {
    $csv = @($results | ConvertTo-Csv -NoTypeInformation)
    $lines = @($results | ForEach-Object { '[{0}] {1} — {2}' -f $_.Status, $_.Check, $_.Detail })
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'phase10-results.csv'), ($csv -join "`n") + "`n")
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'phase10-results.txt'), ($lines -join "`n") + "`n")
}

function Skip-Smoke([string]$Reason) {
    $results.Add([pscustomobject]@{ Status = 'NOT_RUN'; Check = '實機驗證'; Detail = $Reason })
    Write-Results
    Write-Host "尚未驗證：$Reason"
    exit 2
}

# 每次注入之前重新確認輸入與前景，避免拖曳途中使用者回來後仍送出事件。
function Invoke-Drag([int]$FromX, [int]$FromY, [int]$ToX, [int]$ToY, [IntPtr[]]$Allowed) {
    Assert-Target $FromX $FromY $Allowed
    [Phase4Native]::MoveCursor($FromX, $FromY)
    Start-Sleep -Milliseconds 60
    Assert-Target $FromX $FromY $Allowed
    [void][Phase4Native]::LeftDown()
    foreach ($step in 1..6) {
        $nextX = [int]($FromX + ($ToX - $FromX) * $step / 6)
        $nextY = [int]($FromY + ($ToY - $FromY) * $step / 6)
        Assert-Target $nextX $nextY $Allowed
        [Phase4Native]::MoveCursor($nextX, $nextY)
        Start-Sleep -Milliseconds 80
    }
    Assert-Target $ToX $ToY $Allowed
    [void][Phase4Native]::LeftUp()
    Start-Sleep -Milliseconds 250
}

function Assert-Editor([IntPtr]$Editor) {
    Assert-NoUserInput
    if ([Phase4Native]::GetForegroundWindow() -ne $Editor) { Stop-Simulation '編輯器不在前景' }
}

function Invoke-Button([IntPtr]$Window, [string]$AutomationId) {
    Assert-Editor $Window
    $button = Find-EditorElement $Window $AutomationId
    if (-not $button) { Stop-Simulation "找不到按鈕 $AutomationId" }
    Assert-Editor $Window
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 250
}

function Add-Effect([IntPtr]$Editor, $Origin, [string]$Tool, [int]$X, [int]$Y, [int]$Width, [int]$Height) {
    Assert-Editor $Editor
    if (-not (Select-Tool $Editor $Tool)) { Stop-Simulation '無法選擇效果工具' }
    Invoke-Drag ($Origin.X + $X) ($Origin.Y + $Y) ($Origin.X + $X + $Width - 1) ($Origin.Y + $Y + $Height - 1) @($Editor)
}

# 獨立參考演算法：直接列舉每個區塊或每個像素的鄰居，不使用產品的快取與滑動總和。
Add-Type -TypeDefinition @"
using System;
public static class Phase10Reference {
    public static byte[] Apply(byte[] input, int stride, int x, int y, int width, int height, int strength, bool blur) {
        var output = (byte[])input.Clone();
        var region = new byte[width * height * 4];
        for (int yy = 0; yy < height; yy++)
            Array.Copy(input, (y + yy) * stride + x * 4, region, yy * width * 4, width * 4);
        if (blur) {
            for (int pass = 0; pass < 6; pass++) {
                var next = new byte[region.Length];
                for (int yy = 0; yy < height; yy++) for (int xx = 0; xx < width; xx++) {
                    for (int c = 0; c < 3; c++) {
                        int sum = 0, count = 0;
                        for (int d = -strength; d <= strength; d++) {
                            int sx = xx + (pass % 2 == 0 ? d : 0), sy = yy + (pass % 2 == 1 ? d : 0);
                            if (sx >= 0 && sx < width && sy >= 0 && sy < height) { sum += region[(sy * width + sx) * 4 + c]; count++; }
                        }
                        next[(yy * width + xx) * 4 + c] = (byte)Math.Round((double)sum / count, MidpointRounding.AwayFromZero);
                    }
                    next[(yy * width + xx) * 4 + 3] = 255;
                }
                region = next;
            }
        } else {
            var next = new byte[region.Length];
            for (int top = 0; top < height; top += strength) for (int left = 0; left < width; left += strength) {
                int right = Math.Min(width, left + strength), bottom = Math.Min(height, top + strength);
                for (int c = 0; c < 3; c++) {
                    int sum = 0, count = 0;
                    for (int yy = top; yy < bottom; yy++) for (int xx = left; xx < right; xx++) { sum += region[(yy * width + xx) * 4 + c]; count++; }
                    byte mean = (byte)Math.Round((double)sum / count, MidpointRounding.AwayFromZero);
                    for (int yy = top; yy < bottom; yy++) for (int xx = left; xx < right; xx++) { next[(yy * width + xx) * 4 + c] = mean; next[(yy * width + xx) * 4 + 3] = 255; }
                }
            }
            region = next;
        }
        for (int yy = 0; yy < height; yy++) Array.Copy(region, yy * width * 4, output, (y + yy) * stride + x * 4, width * 4);
        return output;
    }
}
"@

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$ExePath = (Resolve-Path -LiteralPath $ExePath).Path
if (@(Get-AppProcesses).Count -gt 0) { Skip-Smoke '已有 ScreenshotApp 執行個體；保留使用者的程式與文件，未送出輸入。' }
$runKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
try { if ($runKey -and $null -ne $runKey.GetValue('ScreenshotApp')) { Skip-Smoke '既有登入啟動設定存在；未變更登錄或啟動受測程式。' } }
finally { if ($runKey) { $runKey.Dispose() } }
if ((Test-HotkeyRegistration) -ne 0) { Skip-Smoke '預設快捷鍵已被其他程式占用。' }
$watch = [Diagnostics.Stopwatch]::StartNew()
while ([Phase4Native]::IdleMilliseconds() -lt $IdleSeconds * 1000) {
    if ($watch.Elapsed.TotalMinutes -ge $MaxIdleWaitMinutes) { Skip-Smoke '等待閒置逾時，未操作桌面。' }
    Start-Sleep -Seconds 1
}

$monitors = @([Phase4Native]::Monitors())
$primary = $monitors | Where-Object { $_[0] -eq 0 -and $_[1] -eq 0 } | Select-Object -First 1
$Pattern = [pscustomobject]@{ X = 0; Y = 0; Width = $primary[2]; Height = $primary[3] }
$backup = [System.Windows.Forms.DataObject]::new()
$current = Invoke-Clipboard { [System.Windows.Forms.Clipboard]::GetDataObject() }
if ($current) {
    foreach ($format in $current.GetFormats($false)) {
        $value = $current.GetData($format, $false)
        if ($null -eq $value) { Skip-Smoke "剪貼簿格式 $format 無法完整備份。" }
        $backup.SetData($format, $false, $value)
    }
}
$script:ownClipboardSequence = [Phase4Native]::GetClipboardSequenceNumber()
$settingsBytes = if (Test-Path -LiteralPath $SettingsPath) { [IO.File]::ReadAllBytes($SettingsPath) } else { $null }
$logBytes = if (Test-Path -LiteralPath $LogPath) { [IO.File]::ReadAllBytes($LogPath) } else { $null }
$dataExisted = Test-Path -LiteralPath $DataDirectory
$logsExisted = Test-Path -LiteralPath $LogDirectory
$app = $null
$patternProcess = $null
$Selection = [pscustomobject]@{ X = 100; Y = 80; Width = 600; Height = 300 }
$script:foregroundRestores = 0
try {
    New-Item -ItemType Directory -Force -Path $DataDirectory | Out-Null
    [IO.File]::WriteAllText($SettingsPath, '{"Hotkey":"Ctrl+Shift+2","StartWithWindows":false,"HistoryEnabled":false}')
    $patternProcess = Start-Process pwsh -PassThru -WindowStyle Hidden -ArgumentList @(
        '-NoProfile', '-File', ('"{0}"' -f (Join-Path $PSScriptRoot 'Show-TestPattern.ps1')),
        '-X', 0, '-Y', 0, '-Width', $Pattern.Width, '-Height', $Pattern.Height)
    if (-not (Show-Pattern)) { throw '圖樣尚未完整顯示，不能取樣' }
    $app = Start-App
    if (-not $app.TrayReady) { throw '受測程式沒有就緒' }
    Add-Info '建置' ((Get-FileHash -LiteralPath $ExePath -Algorithm SHA256).Hash)
    Add-Info '主顯示器' ("$($Pattern.Width)×$($Pattern.Height)，$($primary[4])%")

    # 先等覆蓋層成為前景，才送出選取拖曳。
    Send-Hotkey
    if (-not (Wait-OverlayForeground $app.Process.Id)) { throw '覆蓋層沒有取得前景' }
    $overlay = [Phase4Native]::GetForegroundWindow()
    Invoke-Drag ($Pattern.X + $Selection.X) ($Pattern.Y + $Selection.Y) `
        ($Pattern.X + $Selection.X + $Selection.Width - 1) ($Pattern.Y + $Selection.Y + $Selection.Height - 1) @($overlay)
    $editor = Wait-EditorForeground $app.Process.Id
    if (-not $editor) { throw '選取沒有完成' }
    Hide-Pattern
    $origin = Find-ImageOrigin $editor 0 0 $Selection.Width $Selection.Height
    if (-not $origin) { throw '找不到 100% 畫布位置' }
    $baseline = Copy-ViaButton $editor
    if (-not $baseline) { throw '無法讀取原始圖片' }
    $first = ''
    Add-Result '原始截圖與已知圖樣一致' ([Phase4Native]::CountPatternMismatches($baseline.Png.Bytes, $baseline.Png.Stride, 0, 0, 600, 300, 100, 80, [ref]$first) -eq 0) $first
    Add-Effect $editor $origin 'MosaicToolButton' 40 40 127 93
    Add-Effect $editor $origin 'BlurToolButton' 300 80 131 97
    $copy = Copy-ViaButton $editor
    if (-not $copy) { throw '效果複製失敗' }
    $expected = [Phase10Reference]::Apply($baseline.Png.Bytes, $baseline.Png.Stride, 40, 40, 127, 93, 12, $false)
    $expected = [Phase10Reference]::Apply($expected, $baseline.Png.Stride, 300, 80, 131, 97, 6, $true)
    $different = [Phase4Native]::CountDifferent($copy.Png.Bytes, $copy.Png.Stride, 0, 0, $expected, $baseline.Png.Stride, 0, 0, 600, 300)
    Add-Result '兩種效果範圍符合獨立期望，範圍外保持原圖' ($different -eq 0) "不同像素 $different；包含非完整區塊與三輪模糊邊界"
    Add-Result '剪貼簿 PNG 與 DIB 可視像素相同' (Test-SameImage $copy.Png $copy.Dib) '只比較合成結果'
    $null = Get-EditorPixels $editor 'effects.png'
    $savedPath = Join-Path ([IO.Path]::GetFullPath($EvidenceDirectory)) ('effects-' + [Guid]::NewGuid().ToString('N') + '.png')
    Invoke-Button $editor 'SaveButton'
    $saved = (Complete-FileDialog $app.Process.Id $SaveDialogTitle $savedPath) -and [bool](Wait-Until { Test-Path -LiteralPath $savedPath } 8000)
    Add-Result '另存與剪貼簿 PNG 位元組相同' ($saved -and [Linq.Enumerable]::SequenceEqual([byte[]][IO.File]::ReadAllBytes($savedPath), [byte[]]$copy.PngBytes)) '單一合成影像'
    $samples = [Collections.Generic.List[object]]::new()
    foreach ($cycle in 0..8) {
        Invoke-Button $editor 'UndoButton'
        Invoke-Button $editor 'UndoButton'
        $restored = Copy-ViaButton $editor
        Add-Result "循環 $cycle 復原後恢復原圖" (Test-SameImage $baseline.Png $restored.Png) '兩個效果各一筆命令'
        Invoke-Button $editor 'RedoButton'
        Invoke-Button $editor 'RedoButton'
        $redone = Copy-ViaButton $editor
        Add-Result "循環 $cycle 重做後恢復效果" (Test-SameImage $copy.Png $redone.Png) '重算或快取結果相同'
        $samples.Add((Get-ProcessSample $app.Process $cycle))
    }
    $samples | Export-Csv -LiteralPath (Join-Path $EvidenceDirectory 'resources.csv') -NoTypeInformation
    Add-Result '暖機後 GDI 物件未持續增加' (($samples[-1].Gdi - $samples[1].Gdi) -le 2) "$($samples[1].Gdi) → $($samples[-1].Gdi)"
    Add-Result '暖機後 USER 物件未持續增加' (($samples[-1].User - $samples[1].User) -le 2) "$($samples[1].User) → $($samples[-1].User)"
}
catch {
    Add-Result '實機驗證完整執行' $false $_.Exception.Message
}
finally {
    if ($app -and -not $app.Process.HasExited -and -not [Phase4Native]::UserInputSinceInjection(300)) {
        $owned = @([Phase4Native]::TopLevelWindows([uint32]$app.Process.Id))
        if ($owned -contains [Phase4Native]::GetForegroundWindow()) {
            [void][Phase4Native]::KeyUp(0x11)
            [void][Phase4Native]::LeftUp()
        }
    }
    # 只結束這支腳本建立的處理程序；使用者回來後不再送任何鍵盤滑鼠輸入。
    if ($app -and -not $app.Process.HasExited) { $app.Process.Kill(); $app.Process.WaitForExit() }
    if ($patternProcess -and -not $patternProcess.HasExited) { $patternProcess.Kill(); $patternProcess.WaitForExit() }
    if ($null -ne $settingsBytes) { [IO.File]::WriteAllBytes($SettingsPath, $settingsBytes) }
    elseif (Test-Path -LiteralPath $SettingsPath) { Remove-Item -LiteralPath $SettingsPath }
    if ($null -ne $logBytes) { [IO.File]::WriteAllBytes($LogPath, $logBytes) }
    elseif (Test-Path -LiteralPath $LogPath) { Remove-Item -LiteralPath $LogPath }
    if (-not $logsExisted -and (Test-Path -LiteralPath $LogDirectory) -and -not (Get-ChildItem -LiteralPath $LogDirectory -Force)) { Remove-Item -LiteralPath $LogDirectory }
    if (-not $dataExisted -and (Test-Path -LiteralPath $DataDirectory) -and -not (Get-ChildItem -LiteralPath $DataDirectory -Force)) { Remove-Item -LiteralPath $DataDirectory }
    if ([Phase4Native]::GetClipboardSequenceNumber() -eq $script:ownClipboardSequence) {
        if ($backup.GetFormats($false).Count -gt 0) { Invoke-Clipboard { [System.Windows.Forms.Clipboard]::SetDataObject($backup, $true) } }
        else { Invoke-Clipboard { [System.Windows.Forms.Clipboard]::Clear() } }
    }
    else { Add-Info '剪貼簿' '已由其他程式更新，保留新內容' }
    Write-Results
}
exit $(if (@($results | Where-Object Status -eq 'FAIL').Count -gt 0) { 1 } else { 0 })
