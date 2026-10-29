#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) {
    throw '品質檢查需要 Windows，因為測試包含 WPF 與 Win32。'
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resultsDirectory = Join-Path $repositoryRoot ('artifacts/quality/' + [Guid]::NewGuid().ToString('N'))
Push-Location -LiteralPath $repositoryRoot
try {
    dotnet restore ScreenshotApp.sln --locked-mode --nologo -warnaserror
    if ($LASTEXITCODE -ne 0) { throw "套件還原失敗（結束碼 $LASTEXITCODE）。" }

    dotnet build ScreenshotApp.sln -c Release --no-restore --nologo -warnaserror
    if ($LASTEXITCODE -ne 0) { throw "Release 建置失敗（結束碼 $LASTEXITCODE）。" }

    dotnet test ScreenshotApp.sln -c Release --no-build --no-restore --nologo `
        --logger 'trx;LogFileName=tests.trx' --results-directory $resultsDirectory
    if ($LASTEXITCODE -ne 0) { throw "測試失敗（結束碼 $LASTEXITCODE）。" }

    # 每次使用獨立目錄，避免舊報告讓本次未執行的測試誤判為通過。
    [xml]$report = Get-Content -LiteralPath (Join-Path $resultsDirectory 'tests.trx') -Raw
    $counts = $report.TestRun.ResultSummary.Counters
    if ([int]$counts.total -le 0 -or [int]$counts.passed -ne [int]$counts.total) {
        throw '測試報告必須包含測試，而且全部通過；空測試或跳過的測試不算通過。'
    }

    Write-Host "品質檢查通過：$($counts.passed) 個測試。報告：$resultsDirectory"
}
finally {
    Pop-Location
}
