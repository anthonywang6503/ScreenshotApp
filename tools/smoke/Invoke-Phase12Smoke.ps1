#Requires -Version 7
<#
.SYNOPSIS
    在已知圖樣上驗證磁碟歷史、重新啟動、還原、淘汰與清除；只在閒置時操作桌面。
.DESCRIPTION
    必須先結束既有程式。使用既有煙霧工具的輸入守衛、PrintWindow 與像素比對。
    設定與日誌以記憶體備份；剪貼簿若被其他程式更新就保留新內容。啟動登錄值存在時停止，避免改變登入啟動設定。
    執行期間不要啟動其他指令，以免終端機取得前景。未完成的實機項目不算通過。
#>
param(
    [string]$ExePath = (Join-Path $PSScriptRoot '../../ScreenshotApp/bin/Release/net10.0-windows/ScreenshotApp.exe'),
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot '../../docs/evidence/phase-12'),
    [ValidateRange(90, 3600)][int]$IdleSeconds = 90,
    [ValidateRange(1, 60)][int]$MaxIdleWaitMinutes = 2
)

. (Join-Path $PSScriptRoot 'Invoke-Phase4Smoke.ps1') -LibraryOnly -ExePath $ExePath `
    -EvidenceDirectory $EvidenceDirectory -IdleSeconds $IdleSeconds -MaxIdleWaitMinutes $MaxIdleWaitMinutes

function Write-Results {
    $csv = @($results | ConvertTo-Csv -NoTypeInformation)
    $lines = @($results | ForEach-Object { '[{0}] {1} — {2}' -f $_.Status, $_.Check, $_.Detail })
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'phase12-results.csv'), ($csv -join "`n") + "`n")
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'phase12-results.txt'), ($lines -join "`n") + "`n")
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

$historyRoot = Join-Path $DataDirectory 'history'
$historyExisted = Test-Path -LiteralPath $historyRoot
$ownedEntries = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

function Remember-TestEntries {
    if (-not (Test-Path -LiteralPath $historyRoot)) { return }
    foreach ($directory in Get-ChildItem -LiteralPath $historyRoot -Directory) {
        if ($directory.Name -notmatch '^\d{8}T\d{9}Z_[0-9a-f]{32}$') { throw '出現非預期的歷史子目錄，停止操作。' }
        [void]$ownedEntries.Add($directory.FullName)
    }
}

function Open-HistoryWindow {
    Assert-NoUserInput
    if (-not (Invoke-TrayMenuItem $app.Process '截圖歷史*')) { throw '無法開啟歷史視窗' }
    $window = Wait-Until { Find-Windows $app.Process.Id '截圖歷史' | Select-Object -First 1 } 3000
    if (-not $window) { throw '找不到歷史視窗' }
    if (-not (Wait-Until { [Phase4Native]::GetForegroundWindow() -eq $window } 3000)) { throw '歷史視窗未取得前景' }
    return $window
}

function History-ItemCount([IntPtr]$Window) {
    $list = Find-EditorElement $Window 'EntriesList'
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    return @($list.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)).Count
}

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$ExePath = (Resolve-Path -LiteralPath $ExePath).Path
if (@(Get-AppProcesses).Count -gt 0) { Skip-Smoke '已有 ScreenshotApp 執行個體；保留使用者的程式與文件，未送出輸入。' }
if ($historyExisted -and @(Get-ChildItem -LiteralPath $historyRoot -Force).Count -gt 0) { Skip-Smoke '歷史資料夾不是空的；為避免淘汰或清除使用者資料，本腳本未執行。' }
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
    [IO.File]::WriteAllText($SettingsPath, '{"Hotkey":"Ctrl+Shift+2","StartWithWindows":false,"HistoryEnabled":true,"HistoryLimit":50}')
    $patternProcess = Start-Process pwsh -PassThru -WindowStyle Hidden -ArgumentList @(
        '-NoProfile', '-File', ('"{0}"' -f (Join-Path $PSScriptRoot 'Show-TestPattern.ps1')),
        '-X', 0, '-Y', 0, '-Width', $Pattern.Width, '-Height', $Pattern.Height)
    if (-not (Show-Pattern)) { throw '圖樣尚未完整顯示，不能取樣' }
    $app = Start-App
    if (-not $app.TrayReady) { throw '受測程式沒有就緒' }
    Add-Info '建置' ((Get-FileHash -LiteralPath $ExePath -Algorithm SHA256).Hash)
    Add-Info '主顯示器' ("$($Pattern.Width)×$($Pattern.Height)，$($primary[4])%")

    $expectedFirst = $null
    foreach ($index in 1..3) {
        Send-Hotkey
        if (-not (Wait-OverlayForeground $app.Process.Id)) { throw '覆蓋層沒有取得前景' }
        $overlay = [Phase4Native]::GetForegroundWindow()
        Invoke-Drag ($Selection.X) ($Selection.Y) ($Selection.X + $Selection.Width - 1) ($Selection.Y + $Selection.Height - 1) @($overlay)
        $editor = Wait-EditorForeground $app.Process.Id
        if (-not $editor) { throw '截圖未開啟編輯器' }
        Hide-Pattern
        $origin = Find-ImageOrigin $editor 0 0 $Selection.Width $Selection.Height
        if (-not $origin) { throw '找不到 100% 畫布位置' }
        Add-Rectangle $editor $origin (20 * $index) 30 (20 * $index + 100) 130
        $copied = Copy-ViaButton $editor
        if (-not $copied) { throw '複製失敗' }
        if ($index -eq 1) { $expectedFirst = [Convert]::ToBase64String($copied.Png.Bytes) }
        if (-not (Wait-Until { @(Get-ChildItem -LiteralPath $historyRoot -Filter document.json -Recurse -File).Count -eq $index } 8000)) {
            throw '歷史檔案寫入未完成'
        }
        Remember-TestEntries
        Assert-Editor $editor
        [void](Close-EditorWindow $app.Process.Id $editor)
        if (-not (Show-Pattern)) { throw '無法重新顯示圖樣' }
    }
    Hide-Pattern
    Add-Result '三次截圖保存原圖、縮圖及標註' ($ownedEntries.Count -eq 3 -and @($ownedEntries | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $_ 'original.png')) -or -not (Test-Path -LiteralPath (Join-Path $_ 'thumb.png'))
    }).Count -eq 0) '只在空白歷史資料夾執行'
    Assert-NoUserInput
    if (-not (Invoke-TrayMenuItem $app.Process '結束*') -or -not $app.Process.WaitForExit(8000)) { throw '無法正常結束程式' }
    $app = Start-App
    if (-not $app.TrayReady) { throw '重新啟動失敗' }
    $history = Open-HistoryWindow
    if (-not (Wait-Until { (History-ItemCount $history) -eq 3 } 8000)) { throw '重新啟動後歷史未列出三筆' }
    Add-Result '重新啟動後列出三筆' $true ''
    $null = Get-EditorPixels $history 'history-list.png'
    $list = Find-EditorElement $history 'EntriesList'
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    $items = @($list.FindAll([System.Windows.Automation.TreeScope]::Children, $condition))
    Assert-Editor $history
    $items[2].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Invoke-Button $history 'OpenHistoryButton'
    $editor = Wait-EditorForeground $app.Process.Id
    $restored = Copy-ViaButton $editor
    Add-Result '重新開啟第一筆後合成像素相同' ($restored -and [Convert]::ToBase64String($restored.Png.Bytes) -eq $expectedFirst) '包含原圖與矩形標註'
    Assert-Editor $editor
    [void](Close-EditorWindow $app.Process.Id $editor)
    Assert-NoUserInput
    if (-not (Invoke-TrayMenuItem $app.Process '結束*') -or -not $app.Process.WaitForExit(8000)) { throw '無法正常結束程式' }
    [IO.File]::WriteAllText($SettingsPath, '{"Hotkey":"Ctrl+Shift+2","StartWithWindows":false,"HistoryEnabled":true,"HistoryLimit":2}')
    $app = Start-App
    $history = Open-HistoryWindow
    if (-not (Wait-Until { (History-ItemCount $history) -eq 2 } 8000)) { throw '保留筆數未套用' }
    $oldest = @($ownedEntries | Sort-Object)[0]
    Add-Result '保留兩筆淘汰最舊條目' (-not (Test-Path -LiteralPath $oldest)) ''
    Invoke-Button $history 'DeleteHistoryButton'
    Add-Result '刪除單筆後剩一筆' ([bool](Wait-Until { (History-ItemCount $history) -eq 1 } 5000)) ''
    # InvokePattern 對模態確認可能同步等待；以受守衛的存取鍵開啟，再明確選擇是。
    Assert-Editor $history
    [void][Phase4Native]::PressKeys(0x12, 0x58)
    $confirm = Wait-Until {
        [Phase4Native]::TopLevelWindows([uint32]$app.Process.Id) | Where-Object {
            [Phase4Native]::IsWindowVisible($_) -and [Phase4Native]::ClassOf($_) -eq '#32770'
        } | Select-Object -First 1
    } 3000
    if (-not $confirm) { throw '沒有清除確認視窗' }
    Assert-Editor $confirm
    [void][Phase4Native]::PressKeys(0x12, 0x59)
    Add-Result '確認全部清除後檔案移除' ([bool](Wait-Until { @(Get-ChildItem -LiteralPath $historyRoot -Directory).Count -eq 0 } 5000)) ''
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
    # 根目錄起初必須為空；只清理由本次唯一受測程序建立且已記錄的條目，不使用遞迴刪除。
    foreach ($entry in $ownedEntries) {
        $full = [IO.Path]::GetFullPath($entry)
        $rootPrefix = [IO.Path]::GetFullPath($historyRoot).TrimEnd('\') + '\'
        if (-not $full.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw '拒絕清理歷史根目錄外的路徑' }
        if (Test-Path -LiteralPath $full) {
            $directory = Get-Item -LiteralPath $full -Force
            if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw '拒絕清理連結目錄' }
            foreach ($file in Get-ChildItem -LiteralPath $full -File -Force) {
                if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw '拒絕清理連結檔案' }
                Remove-Item -LiteralPath $file.FullName
            }
            Remove-Item -LiteralPath $full
        }
    }
    if (-not $historyExisted -and (Test-Path -LiteralPath $historyRoot) -and @(Get-ChildItem -LiteralPath $historyRoot -Force).Count -eq 0) {
        Remove-Item -LiteralPath $historyRoot
    }
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
