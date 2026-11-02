#Requires -Version 7
<#
.SYNOPSIS
    在已知圖樣上驗證手動／自動長截圖、固定標頭與取消；只在閒置時操作桌面。
.DESCRIPTION
    必須先結束既有程式。使用既有煙霧工具的輸入守衛、PrintWindow 與像素比對。
    設定與日誌以記憶體備份；剪貼簿若被其他程式更新就保留新內容。啟動登錄值存在時停止，避免改變登入啟動設定。
    執行期間不要啟動其他指令，以免終端機取得前景。未完成的實機項目不算通過。
#>
param(
    [string]$ExePath = (Join-Path $PSScriptRoot '../../ScreenshotApp/bin/Release/net10.0-windows/ScreenshotApp.exe'),
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot '../../docs/evidence/phase-13'),
    [ValidateRange(90, 3600)][int]$IdleSeconds = 90,
    [ValidateRange(1, 60)][int]$MaxIdleWaitMinutes = 2
)

. (Join-Path $PSScriptRoot 'Invoke-Phase4Smoke.ps1') -LibraryOnly -ExePath $ExePath `
    -EvidenceDirectory $EvidenceDirectory -IdleSeconds $IdleSeconds -MaxIdleWaitMinutes $MaxIdleWaitMinutes

function Write-Results {
    $csv = @($results | ConvertTo-Csv -NoTypeInformation)
    $lines = @($results | ForEach-Object { '[{0}] {1} — {2}' -f $_.Status, $_.Check, $_.Detail })
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'phase13-results.csv'), ($csv -join "`n") + "`n")
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'phase13-results.txt'), ($lines -join "`n") + "`n")
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

function Invoke-Button([IntPtr]$Window, [string]$AutomationId) {
    Assert-NoUserInput
    $owned = @([Phase4Native]::TopLevelWindows([uint32]$app.Process.Id))
    if ($owned -notcontains $Window) { Stop-Simulation '目標不屬於受測程式' }
    $foreground = [Phase4Native]::GetForegroundWindow()
    if ($owned -notcontains $foreground -and @(Get-PatternWindows) -notcontains $foreground) { Stop-Simulation '前景已離開測試視窗' }
    $button = Find-EditorElement $Window $AutomationId
    if (-not $button -or -not $button.Current.IsEnabled) { throw "找不到可操作按鈕 $AutomationId" }
    Assert-NoUserInput
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 250
}

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$ExePath = (Resolve-Path -LiteralPath $ExePath).Path
if (@(Get-AppProcesses).Count -gt 0) { Skip-Smoke '已有 ScreenshotApp 執行個體；保留使用者的程式與文件，未送出輸入。' }
$runKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
try { if ($runKey -and $null -ne $runKey.GetValue('ScreenshotApp')) { Skip-Smoke '既有登入啟動設定存在；未變更登錄或啟動受測程式。' } }
finally { if ($runKey) { $runKey.Dispose() } }
if ((Test-HotkeyRegistration) -ne 0) { Skip-Smoke '預設快捷鍵已被其他程式占用。' }
if (-not [Phase4Native]::RegisterHotKey([IntPtr]::Zero, 0x5313, 0x4006, 0x83)) { Skip-Smoke '長截圖測試快捷鍵已被占用。' }
[void][Phase4Native]::UnregisterHotKey([IntPtr]::Zero, 0x5313)
$watch = [Diagnostics.Stopwatch]::StartNew()
while ([Phase4Native]::IdleMilliseconds() -lt $IdleSeconds * 1000) {
    if ($watch.Elapsed.TotalMinutes -ge $MaxIdleWaitMinutes) { Skip-Smoke '等待閒置逾時，未操作桌面。' }
    Start-Sleep -Seconds 1
}

$Pattern = [pscustomobject]@{ X = 80; Y = 80; Width = 600; Height = 400 }
$monitors = @([Phase4Native]::Monitors())
$primary = $monitors | Where-Object { $_[0] -eq 0 -and $_[1] -eq 0 } | Select-Object -First 1
if (-not $primary -or $primary[2] -lt 1200 -or $primary[3] -lt 900) { Skip-Smoke '主顯示器至少需要 1200×900 實體像素。' }
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

$samples = [Collections.Generic.List[object]]::new()

function Start-ScrollingPattern {
    Assert-NoUserInput
    if ($script:patternProcess -and -not $script:patternProcess.HasExited) {
        $script:patternProcess.Kill(); $script:patternProcess.WaitForExit()
    }
    $script:patternProcess = Start-Process pwsh -PassThru -WindowStyle Hidden -ArgumentList @(
        '-NoProfile', '-File', ('"{0}"' -f (Join-Path $PSScriptRoot 'Show-TestPattern.ps1')),
        '-X', 80, '-Y', 80, '-Width', 600, '-Height', 400, '-Scrollable', '-ContentHeight', 1600, '-HeaderHeight', 24, '-ScrollStep', 80)
    if (-not (Show-Pattern)) { throw '可捲動圖樣尚未完整顯示' }
}

function Start-Scrolling {
    Start-ScrollingPattern
    Assert-NoUserInput
    # 前置註冊成功且只有本腳本的受測程序，才能送出測試快捷鍵。
    if ([Phase4Native]::RegisterHotKey([IntPtr]::Zero, 0x5313, 0x4006, 0x83)) {
        [void][Phase4Native]::UnregisterHotKey([IntPtr]::Zero, 0x5313)
        throw '長截圖快捷鍵未註冊'
    }
    [void][Phase4Native]::PressKeys(0x11, 0x10, 0x83)
    if (-not (Wait-OverlayForeground $app.Process.Id)) { throw '覆蓋層沒有取得前景' }
    $overlay = [Phase4Native]::GetForegroundWindow()
    Invoke-Drag 80 80 679 479 @($overlay)
    $panel = Wait-AppWindow $app.Process.Id '長截圖'
    if (-not $panel) { throw '未出現長截圖面板' }
    Start-Sleep -Milliseconds 650
    return $panel
}

function Check-Output([IntPtr]$Editor, [int]$Height, [string]$Name) {
    Assert-NoUserInput
    Hide-Pattern
    $images = Copy-ViaButton $Editor
    if (-not $images) { throw '無法取得長截圖輸出' }
    $size = $images.Png.Width -eq 600 -and $images.Png.Height -eq $Height
    Add-Result "$Name 尺寸" $size "預期 600×$Height，實際 $($images.Png.Width)×$($images.Png.Height)"
    $first = ''
    $mismatches = if ($size) { [Phase4Native]::CountPatternMismatches($images.Png.Bytes, $images.Png.Stride, 0, 0, 600, $Height, 0, 0, [ref]$first) } else { -1 }
    Add-Result "$Name 逐像素連續且標頭只出現一次" ($mismatches -eq 0) "$mismatches；$first"
    Add-Result "$Name PNG 與 DIB 相同" (Test-SameImage $images.Png $images.Dib) '合成輸出'
    $null = Get-EditorPixels $Editor "$Name.png"
    Assert-NoUserInput
    if ((Close-EditorWindow $app.Process.Id $Editor) -ne 'closed') { throw '已複製文件仍要求捨棄' }
}

try {
    [IO.Directory]::CreateDirectory($DataDirectory) | Out-Null
    [IO.File]::WriteAllText($SettingsPath, '{"Hotkey":"Ctrl+Shift+2","ScrollHotkey":"Ctrl+Shift+F20","StartWithWindows":false,"HistoryEnabled":false}')
    $app = Start-App
    if (-not $app.TrayReady) { throw '受測程式沒有就緒' }
    Add-Info '建置' ((Get-FileHash -LiteralPath $ExePath -Algorithm SHA256).Hash)
    Add-Info '桌面縮放' "$($primary[4])%"
    $panel = Start-Scrolling
    $target = @(Get-PatternWindows)[0]
    foreach ($step in 1..3) {
        Assert-NoUserInput
        if (@($target, $panel) -notcontains [Phase4Native]::GetForegroundWindow() -or [Phase4Native]::RootWindowAt(380, 280) -ne $target) { Stop-Simulation '圖樣已失去前景或被遮住' }
        [void][Phase4Native]::PostMessage($target, 0x020A, [IntPtr](-120 -shl 16), [IntPtr]((280 -shl 16) -bor 380))
        $expected = "600 × $([int](400 + 80 * $step)) 像素"
        if (-not (Wait-Until { Assert-NoUserInput; (Get-EditorText $panel 'HeightText') -eq $expected } 7000)) { throw "手動拼接未達 $expected" }
    }
    $null = Get-EditorPixels $panel 'manual-panel.png'
    Invoke-Button $panel 'FinishButton'
    $editor = Wait-EditorForeground $app.Process.Id
    if (-not $editor) { throw '手動完成後沒有編輯器' }
    Check-Output $editor 640 'manual'

    # 取消後不得殘留範圍框與面板；暖機後觀察資源數。
    foreach ($cycle in 0..3) {
        $panel = Start-Scrolling
        Invoke-Button $panel 'CancelCaptureButton'
        if (-not (Wait-Until { @(Find-Windows $app.Process.Id '長截圖').Count -eq 0 -and @(Find-Windows $app.Process.Id '長截圖範圍').Count -eq 0 } 5000)) { throw '取消後有殘留視窗' }
        Add-Result "取消 $cycle 不留下框線或面板" $true '沒有新編輯器'
        if (Find-Editor $app.Process.Id) { throw '取消不應開啟編輯器' }
        $samples.Add((Get-ProcessSample $app.Process $cycle))
    }
    Add-Result '取消循環 GDI／USER 不持續成長' ($samples[3].Gdi -le $samples[0].Gdi + 2 -and $samples[3].User -le $samples[0].User + 2) '暖機後三次循環，允許框架最多兩個快取物件'
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'resources.csv'), (($samples | ConvertTo-Csv -NoTypeInformation) -join "`n") + "`n")

    $panel = Start-Scrolling
    Invoke-Button $panel 'AutomaticButton'
    $editor = Wait-Until { Assert-NoUserInput; Find-Editor $app.Process.Id } 45000
    if (-not $editor) { throw '自動捲動未在時間內完成；保留失敗，不改成人工完成' }
    Check-Output $editor 1600 'automatic'
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
