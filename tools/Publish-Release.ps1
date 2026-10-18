#Requires -Version 7
<#
.SYNOPSIS
    發佈 ScreenshotApp 的 Windows 11 x64 自包含版本並建立 ZIP。

.DESCRIPTION
    在儲存庫的 publish 資料夾（不納入版本控制）產生兩種版本：
      - 資料夾版：publish/ScreenshotApp-<版本>-win-x64/ 與同名 ZIP。
      - 單檔版：publish/ScreenshotApp-<版本>-win-x64-singlefile/ 與同名 ZIP；WPF 原生程式庫內含於執行檔，
        第一次執行時解壓到暫存資料夾。
    兩者都附上 .NET Runtime 與 Windows Desktop Runtime 的授權檔（licenses/）、第三方聲明、使用說明與已知限制。
    ZIP 以發佈資料夾本身為根目錄。版本取自專案檔的 Version；每次執行都先刪除同名的舊輸出。

.EXAMPLE
    pwsh -NoProfile -File tools/Publish-Release.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'ScreenshotApp/ScreenshotApp.csproj'
$publishRoot = Join-Path $root 'publish'

function Invoke-DotNet([string[]]$Arguments) {
    $output = & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        $output | Write-Host
        throw "dotnet $($Arguments[0]) 失敗（結束代碼 $LASTEXITCODE）"
    }
    return $output
}

# 版本與自包含發佈實際使用的 Runtime 套件位置（授權檔在套件根目錄）。
$query = Invoke-DotNet @(
    'msbuild', $project, '-restore', '-t:ResolveFrameworkReferences', '-getProperty:Version', '-getItem:ResolvedRuntimePack',
    '-p:Configuration=Release', '-p:RuntimeIdentifier=win-x64', '-p:SelfContained=true') | Out-String | ConvertFrom-Json
$version = $query.Properties.Version
$runtimePacks = @($query.Items.ResolvedRuntimePack | Where-Object { $_.FrameworkName -in 'Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App' })
if ($runtimePacks.Count -ne 2) { throw '找不到 .NET Runtime 與 Windows Desktop Runtime 套件' }

$variants = @(
    @{ Name = "ScreenshotApp-$version-win-x64"; Properties = @() },
    @{ Name = "ScreenshotApp-$version-win-x64-singlefile"; Properties = @('-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true') }
)

foreach ($variant in $variants) {
    $output = Join-Path $publishRoot $variant.Name
    $zip = "$output.zip"
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }

    $null = Invoke-DotNet (@('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $output) + $variant.Properties)

    $licenses = New-Item -ItemType Directory -Path (Join-Path $output 'licenses')
    foreach ($pack in $runtimePacks) {
        Get-ChildItem -LiteralPath $pack.PackageDirectory -File |
            Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)(\.txt)?$' } |
            ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $licenses "$($pack.FrameworkName).$($_.BaseName).txt") }
    }
    Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES.md'), (Join-Path $root 'docs/USER_GUIDE.md'), (Join-Path $root 'docs/KNOWN_LIMITATIONS.md') -Destination $output

    Compress-Archive -LiteralPath $output -DestinationPath $zip
    $files = @(Get-ChildItem -LiteralPath $output -Recurse -File)
    [pscustomobject]@{
        Output = $output
        Files = $files.Count
        SizeMB = [math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1)
        Zip = $zip
        ZipMB = [math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 1)
    }
}
