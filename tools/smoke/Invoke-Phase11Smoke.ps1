#Requires -Version 7
<#
.SYNOPSIS
    在已知圖樣上驗證尺規、覆蓋層量測、編輯器取色與復原；只在閒置時操作桌面。
.DESCRIPTION
    必須先結束既有程式。使用既有煙霧工具的輸入守衛、PrintWindow 與像素比對。
    設定與日誌以記憶體備份；剪貼簿若被其他程式更新就保留新內容。啟動登錄值存在時停止，避免改變登入啟動設定。
    執行期間不要啟動其他指令，以免終端機取得前景。未完成的實機項目不算通過。
#>
param(
    [string]$ExePath = (Join-Path $PSScriptRoot '../../ScreenshotApp/bin/Release/net10.0-windows/ScreenshotApp.exe'),
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot '../../docs/evidence/phase-11'),
    [ValidateRange(90, 3600)][int]$IdleSeconds = 90,
    [ValidateRange(1, 60)][int]$MaxIdleWaitMinutes = 2
)

. (Join-Path $PSScriptRoot 'Invoke-Phase4Smoke.ps1') -LibraryOnly -ExePath $ExePath `
    -EvidenceDirectory $EvidenceDirectory -IdleSeconds $IdleSeconds -MaxIdleWaitMinutes $MaxIdleWaitMinutes

function Write-Results {
    $csv = @($results | ConvertTo-Csv -NoTypeInformation)
    $lines = @($results | ForEach-Object { '[{0}] {1} — {2}' -f $_.Status, $_.Check, $_.Detail })
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'phase11-results.csv'), ($csv -join "`n") + "`n")
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'phase11-results.txt'), ($lines -join "`n") + "`n")
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

function Send-GuardedKey([IntPtr]$Window, [ushort]$Key) {
    Assert-Editor $Window
    [void][Phase4Native]::KeyDown($Key)
    Start-Sleep -Milliseconds 50
    Assert-Editor $Window
    [void][Phase4Native]::KeyUp($Key)
    Start-Sleep -Milliseconds 250
}

function Read-Label([IntPtr]$Window, [string]$Name) {
    $element = Find-EditorElement $Window $Name
    if (-not $element) { throw "找不到標籤 $Name" }
    return $element.Current.Name
}

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
    Send-GuardedKey $overlay 0x52
    Invoke-Drag 150 150 450 550 @($overlay)
    Add-Result '覆蓋層量測 300／400／500 三角形，放開後保留' ((Read-Label $overlay 'MeasurementText') -eq '長度 500 px　Δx 300　Δy 400　301 × 401 px') '像素中心距離與包含端點的寬高'
    $null = Get-EditorPixels $overlay 'measurement.png'
    Send-GuardedKey $overlay 0x52
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
    Assert-Editor $editor
    if (-not (Select-Tool $editor 'RulerToolButton')) { throw '無法選取尺規' }
    Invoke-Drag ($origin.X + 50) ($origin.Y + 80) ($origin.X + 350) ($origin.Y + 80) @($editor)
    Add-Result '編輯器尺規長度 300px' ((Read-Label $editor 'MessageText') -eq '長度 300 px　Δx 300　Δy 0') '與覆蓋層使用同一量測定義'
    $ruler = Copy-ViaButton $editor
    if (-not $ruler) { throw '尺規複製失敗' }
    $first = ''
    $changed = [Phase4Native]::CountPatternMismatches($ruler.Png.Bytes, $ruler.Png.Stride, 40, 65, 320, 30, 140, 145, [ref]$first)
    Add-Result '尺規線段與標籤包含在輸出' ($changed -gt 300) "與已知圖樣不同的像素：$changed"
    $outside = [Phase4Native]::CountPatternMismatches($ruler.Png.Bytes, $ruler.Png.Stride, 0, 120, 600, 180, 100, 200, [ref]$first)
    Add-Result '尺規範圍外維持原圖' ($outside -eq 0) $first
    Add-Result '尺規 PNG 與 DIB 相同' (Test-SameImage $ruler.Png $ruler.Dib) '只使用合成結果'
    $null = Get-EditorPixels $editor 'ruler.png'
    Invoke-Button $editor 'UndoButton'
    $restored = Copy-ViaButton $editor
    Add-Result '尺規一次復原回到原圖' (Test-SameImage $baseline.Png $restored.Png) '單一建立命令'

    Assert-Editor $editor
    if (-not (Select-Tool $editor 'RectangleToolButton')) { throw '無法選擇矩形' }
    Assert-Editor $editor
    if (-not (Select-Tool $editor 'EyedropperToolButton')) { throw '無法選擇取色' }
    $pickX = 25; $pickY = 25
    Invoke-Drag ($origin.X + $pickX) ($origin.Y + $pickY) ($origin.X + $pickX) ($origin.Y + $pickY) @($editor)
    $offset = $pickY * $baseline.Png.Stride + $pickX * 4
    $rgb = $baseline.Png.Bytes[$offset..($offset + 2)]
    $hex = '#{0:X2}{1:X2}{2:X2}' -f $rgb[2], $rgb[1], $rgb[0]
    Add-Result '取色狀態列顯示原圖色碼' ((Read-Label $editor 'MessageText') -eq "已取色：$hex") $hex
    $rectangle = Find-EditorElement $editor 'RectangleToolButton'
    Add-Result '取色後回到原矩形工具' ($rectangle.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected) '不再切換工具即可繼續畫圖'
    Invoke-Drag ($origin.X + 50) ($origin.Y + 150) ($origin.X + 150) ($origin.Y + 220) @($editor)
    $picked = Copy-ViaButton $editor
    if (-not $picked) { throw '取色矩形複製失敗' }
    $borderOffset = 150 * $picked.Png.Stride + 80 * 4
    Add-Result '新矩形使用取樣色彩' ([Linq.Enumerable]::SequenceEqual([byte[]]$rgb, [byte[]]$picked.Png.Bytes[$borderOffset..($borderOffset + 2)])) '比對矩形上邊完整 RGB'
    $null = Get-EditorPixels $editor 'eyedropper.png'

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
