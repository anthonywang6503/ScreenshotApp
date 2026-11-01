#Requires -Version 7
<#
.SYNOPSIS
    在已知圖樣上驗證放大鏡、Ctrl 輸入、精確選取與文字取色；只在閒置時操作桌面。
.DESCRIPTION
    必須先結束既有程式。使用既有煙霧工具的輸入守衛、PrintWindow 與像素比對。
    設定與日誌以記憶體備份；剪貼簿若被其他程式更新就保留新內容。啟動登錄值存在時停止，避免改變登入啟動設定。
    執行期間不要啟動其他指令，以免終端機取得前景。未完成的實機項目不算通過。
#>
param(
    [string]$ExePath = (Join-Path $PSScriptRoot '../../ScreenshotApp/bin/Release/net10.0-windows/ScreenshotApp.exe'),
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot '../../docs/evidence/phase-09'),
    [ValidateRange(90, 3600)][int]$IdleSeconds = 90,
    [ValidateRange(1, 60)][int]$MaxIdleWaitMinutes = 2
)

. (Join-Path $PSScriptRoot 'Invoke-Phase4Smoke.ps1') -LibraryOnly -ExePath $ExePath `
    -EvidenceDirectory $EvidenceDirectory -IdleSeconds $IdleSeconds -MaxIdleWaitMinutes $MaxIdleWaitMinutes

function Write-Results {
    $csv = @($results | ConvertTo-Csv -NoTypeInformation)
    $lines = @($results | ForEach-Object { '[{0}] {1} — {2}' -f $_.Status, $_.Check, $_.Detail })
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'phase9-results.csv'), ($csv -join "`n") + "`n")
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'phase9-results.txt'), ($lines -join "`n") + "`n")
}

function Skip-Smoke([string]$Reason) {
    $results.Add([pscustomobject]@{ Status = 'NOT_RUN'; Check = '實機驗證'; Detail = $Reason })
    Write-Results
    Write-Host "尚未驗證：$Reason"
    exit 2
}

function Assert-Overlay([IntPtr]$Overlay) {
    Assert-NoUserInput
    if ([Phase4Native]::GetForegroundWindow() -ne $Overlay) { Stop-Simulation '覆蓋層不在前景' }
}

function Set-Control([IntPtr]$Overlay, [bool]$Down) {
    Assert-Overlay $Overlay
    if ($Down) { [void][Phase4Native]::KeyDown(0x11) }
    else { [void][Phase4Native]::KeyUp(0x11) }
    Start-Sleep -Milliseconds 150
}

function Move-ToPixel([IntPtr]$Overlay, [int]$X, [int]$Y) {
    Assert-Target $X $Y @($Overlay)
    [Phase4Native]::MoveCursor($X, $Y)
    Start-Sleep -Milliseconds 160
}

function Check-Magnifier([IntPtr]$Overlay, [int]$X, [int]$Y, [string]$Name) {
    $element = Find-EditorElement $Overlay 'MagnifierImage'
    if (-not $element -or $element.Current.IsOffscreen) { throw '放大鏡沒有顯示' }
    $bounds = $element.Current.BoundingRectangle
    $shot = Get-EditorPixels $Overlay "$Name.png"
    $px = [int][math]::Round($bounds.X) - $shot.Rect.X
    $py = [int][math]::Round($bounds.Y) - $shot.Rect.Y
    $mismatches = 0
    for ($row = 0; $row -lt 180; $row++) {
        for ($col = 0; $col -lt 180; $col++) {
            $sx = $X - $Pattern.X - 7 + [int][math]::Floor($col / 12)
            $sy = $Y - $Pattern.Y - 7 + [int][math]::Floor($row / 12)
            $expected = if ($sx -lt 0 -or $sy -lt 0 -or $sx -ge $Pattern.Width -or $sy -ge $Pattern.Height) {
                0x808080
            } else { [Phase4Native]::PatternColor($sx, $sy) }
            if ($col -ge 84 -and $col -lt 96 -and $row -ge 84 -and $row -lt 96) {
                if ($col -eq 84 -or $col -eq 95 -or $row -eq 84 -or $row -eq 95) { $expected = 0 }
                elseif ($col -eq 85 -or $col -eq 94 -or $row -eq 85 -or $row -eq 94) { $expected = 0xFFFFFF }
            }
            if ([Phase4Native]::Rgb($shot.Pixels.Bytes, $shot.Pixels.Stride, $px + $col, $py + $row) -ne $expected) { $mismatches++ }
        }
    }
    Add-Result "$Name 每格與中心框精確" ($mismatches -eq 0 -and [math]::Abs($bounds.Width - 180) -lt 1) "不同像素 $mismatches；格寬 12 實體像素"
    $label = Find-EditorElement $Overlay 'MagnifierText'
    $code = '#{0:X6}' -f [Phase4Native]::PatternColor(($X - $Pattern.X), ($Y - $Pattern.Y))
    Add-Result "$Name 座標與色碼" ($label.Current.Name -eq "($($X - $Pattern.X), $($Y - $Pattern.Y)) px`n$code") $label.Current.Name
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

    # 保留快捷鍵中的 Ctrl，確認進入覆蓋層不會直接顯示放大鏡。
    Assert-NoUserInput
    [Phase4Native]::MoveCursor(400, 300)
    if ((Test-HotkeyRegistration) -ne $ErrorHotkeyAlreadyRegistered) { throw '快捷鍵未註冊' }
    [void][Phase4Native]::KeyDown(0x11)
    Assert-NoUserInput
    [void][Phase4Native]::PressKeys(0x10, 0x32)
    if (-not (Wait-OverlayForeground $app.Process.Id)) { throw '覆蓋層沒有取得前景' }
    $overlay = [Phase4Native]::GetForegroundWindow()
    $before = Find-EditorElement $overlay 'MagnifierImage' 200
    Add-Result '快捷鍵 Ctrl 不觸發放大鏡' (-not $before -or $before.Current.IsOffscreen) '必須放開再按下'
    Set-Control $overlay $false
    Set-Control $overlay $true
    Check-Magnifier $overlay 400 300 'center'
    foreach ($point in @(@(0, 0), @(($Pattern.Width - 1), 0), @(0, ($Pattern.Height - 1)), @(($Pattern.Width - 1), ($Pattern.Height - 1)))) {
        Move-ToPixel $overlay $point[0] $point[1]
        Check-Magnifier $overlay $point[0] $point[1] ("corner-$($point[0])-$($point[1])")
    }
    Set-Control $overlay $false
    $after = Find-EditorElement $overlay 'MagnifierImage' 200
    Add-Result '放開 Ctrl 隱藏' (-not $after -or $after.Current.IsOffscreen) '四角移動後放開'

    Move-ToPixel $overlay 400 300
    Set-Control $overlay $true
    Assert-Target 400 300 @($overlay)
    [void][Phase4Native]::LeftDown()
    Move-ToPixel $overlay 419 309
    Check-Magnifier $overlay 419 309 'drag'
    Assert-Overlay $overlay
    [void][Phase4Native]::LeftUp()
    $editor = Wait-EditorForeground $app.Process.Id
    Assert-NoUserInput
    if (-not $editor) { throw '選取沒有完成' }
    [void][Phase4Native]::KeyUp(0x11)
    Hide-Pattern
    $images = Copy-ViaButton $editor
    $first = ''
    $exact = $images -and $images.Png.Width -eq 20 -and $images.Png.Height -eq 10
    if ($exact) { $exact = [Phase4Native]::CountPatternMismatches($images.Png.Bytes, $images.Png.Stride, 0, 0, 20, 10, 400, 300, [ref]$first) -eq 0 }
    Add-Result '放大鏡拖曳後輸出含兩端像素' $exact "20×10；$first"

    if (-not (Show-Pattern)) { throw '取色前圖樣不完整' }
    Send-Hotkey
    if (-not (Wait-OverlayForeground $app.Process.Id)) { throw '取色覆蓋層沒有取得前景' }
    $overlay = [Phase4Native]::GetForegroundWindow()
    Move-ToPixel $overlay 251 197
    Assert-Overlay $overlay
    [void][Phase4Native]::PressKeys(0x43)
    $text = Wait-Until { Invoke-Clipboard { if ([System.Windows.Forms.Clipboard]::ContainsText()) { [System.Windows.Forms.Clipboard]::GetText() } } } 5000
    $script:ownClipboardSequence = [Phase4Native]::GetClipboardSequenceNumber()
    Add-Result 'C 複製快照色碼' ($text -eq ('#{0:X6}' -f [Phase4Native]::PatternColor(251, 197))) $text
    Add-Result '取色關閉覆蓋層並恢復同一編輯視窗' ((Wait-NoOverlays $app.Process.Id) -and (Find-Editor $app.Process.Id) -eq $editor) '沒有建立新視窗'
    Hide-Pattern
    $again = Copy-ViaButton $editor
    Add-Result '取色後原文件保持不變' (Test-SameImage $images.Png $again.Png) '前後複製的 PNG 像素一致'
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
