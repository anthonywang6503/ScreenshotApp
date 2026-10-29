#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$fixtureRoot = Join-Path $repositoryRoot ('artifacts/quality-self-test/' + [Guid]::NewGuid().ToString('N'))
$tracked = git -C $repositoryRoot -c core.quotepath=false ls-files
if ($LASTEXITCODE -ne 0) { throw '無法取得測試副本的來源檔案。' }

$files = @($tracked | Where-Object { $_ -match '^(ScreenshotApp/|ScreenshotApp.Tests/)' }) + @(
    'ScreenshotApp.sln', 'nuget.config', 'global.json',
    'ScreenshotApp/packages.lock.json', 'ScreenshotApp.Tests/packages.lock.json', 'tools/Invoke-Quality.ps1'
)
foreach ($relativePath in ($files | Sort-Object -Unique)) {
    $destination = Join-Path $fixtureRoot $relativePath
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    [IO.File]::Copy((Join-Path $repositoryRoot $relativePath), $destination)
}

function Assert-Rejected([string]$Name, [string]$ExpectedDiagnostic) {
    $log = Join-Path $fixtureRoot "$Name.log"
    & pwsh -NoProfile -File (Join-Path $fixtureRoot 'tools/Invoke-Quality.ps1') *> $log
    $exitCode = $LASTEXITCODE
    $output = Get-Content -LiteralPath $log -Raw
    if ($exitCode -eq 0 -or $output -notmatch $ExpectedDiagnostic) {
        throw "負向驗證 $Name 未以預期原因失敗。結束碼：$exitCode；紀錄：$log"
    }

    Write-Host "$Name 通過：品質檢查拒絕錯誤輸入。"
}

# 故障只注入獨立副本；來源檔案與既有測試完全不改寫。
$warningPath = Join-Path $fixtureRoot 'ScreenshotApp/QualityProbe.cs'
[IO.File]::WriteAllText($warningPath, "#warning 品質檢查警告驗證`n")
Assert-Rejected 'warning' 'CS1030'
[IO.File]::Delete($warningPath)

$failurePath = Join-Path $fixtureRoot 'ScreenshotApp.Tests/QualityProbeTests.cs'
[IO.File]::WriteAllText($failurePath, @'
namespace ScreenshotApp.Tests;
public sealed class QualityProbeTests
{
    [Xunit.Fact]
    public void MustFail() => Xunit.Assert.Fail("品質檢查失敗驗證");
}
'@)
Assert-Rejected 'failed-test' 'Failed.*QualityProbeTests.MustFail'

[IO.File]::WriteAllText($failurePath, @'
namespace ScreenshotApp.Tests;
public sealed class QualityProbeTests
{
    [Xunit.Fact(Skip = "品質檢查跳過驗證")]
    public void MustNotBeSkipped() { }
}
'@)
Assert-Rejected 'skipped-test' '空測試或跳過的測試不算通過'
[IO.File]::Delete($failurePath)

$projectPath = Join-Path $fixtureRoot 'ScreenshotApp.Tests/ScreenshotApp.Tests.csproj'
$project = [IO.File]::ReadAllText($projectPath)
$changedProject = $project.Replace('Version="2.9.3"', 'Version="2.9.2"')
if ($changedProject -eq $project) { throw '請更新鎖定套件的負向驗證版本。' }
[IO.File]::WriteAllText($projectPath, $changedProject)
Assert-Rejected 'lock-drift' 'NU1004'

Write-Host "四項品質檢查負向驗證通過。獨立副本與紀錄：$fixtureRoot"
