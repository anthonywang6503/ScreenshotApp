#Requires -Version 7.0

<#
.SYNOPSIS
    產生 ScreenshotApp 的多尺寸應用程式圖示（.ico）。

.DESCRIPTION
    以 WPF 向量繪圖逐一繪製 16、20、24、32、40、48、64、256 像素影像，不從大圖縮小。
    每個尺寸使用專屬版面，直線邊緣皆落在整數座標以對齊像素格，曲線（圓角、紅點）以抗鋸齒呈現。
    16～64 以 32 位元 BGRA DIB 儲存，256 以 PNG 儲存，ICO 二進位結構由本腳本自行組出。

.PARAMETER OutputPath
    輸出的 .ico 路徑；相對路徑以目前位置為準，上層目錄不存在時會自動建立。

.EXAMPLE
    pwsh -NoProfile -File tools/New-AppIcon.ps1 -OutputPath ScreenshotApp/Resources/app.ico
#>
using namespace System.Windows
using namespace System.Windows.Media
using namespace System.Windows.Media.Imaging

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$outputFile = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($OutputPath)

# WPF 繪圖必須在 STA 執行緒進行；pwsh 預設為 STA，若不是則以 STA 重新執行本腳本
if ([System.Threading.Thread]::CurrentThread.GetApartmentState() -ne [System.Threading.ApartmentState]::STA) {
    pwsh -NoProfile -STA -File $PSCommandPath -OutputPath $outputFile
    exit $LASTEXITCODE
}

Add-Type -AssemblyName PresentationCore, WindowsBase

# 各尺寸版面（單位：像素）
#   Plate：底板外距    Radius：底板圓角    Box：四角括號外緣距畫布邊緣
#   Stroke：括號粗細   Arm：括號臂長（含轉角）
#   Dot：紅點直徑（小尺寸只保留四角與紅點）
#   Mark：紅色標註框的寬、高、線寬、外圓角（置中）
$layouts = @(
    @{ Size = 16;  Plate = 0;  Radius = 4;  Box = 2;  Stroke = 2;  Arm = 4;  Dot = 4; Mark = $null }
    @{ Size = 20;  Plate = 0;  Radius = 4;  Box = 3;  Stroke = 2;  Arm = 5;  Dot = 6; Mark = $null }
    @{ Size = 24;  Plate = 1;  Radius = 4;  Box = 4;  Stroke = 2;  Arm = 6;  Dot = 6; Mark = $null }
    @{ Size = 32;  Plate = 1;  Radius = 6;  Box = 5;  Stroke = 2;  Arm = 8;  Dot = 0; Mark = 14, 10, 2, 0 }
    @{ Size = 40;  Plate = 2;  Radius = 7;  Box = 7;  Stroke = 3;  Arm = 9;  Dot = 0; Mark = 16, 12, 2, 0 }
    @{ Size = 48;  Plate = 2;  Radius = 9;  Box = 8;  Stroke = 3;  Arm = 11; Dot = 0; Mark = 20, 14, 3, 1 }
    @{ Size = 64;  Plate = 3;  Radius = 12; Box = 11; Stroke = 4;  Arm = 14; Dot = 0; Mark = 26, 18, 3, 2 }
    @{ Size = 256; Plate = 10; Radius = 50; Box = 46; Stroke = 16; Arm = 56; Dot = 0; Mark = 100, 72, 12, 8 }
)

$plateBrush = [LinearGradientBrush]::new([Color]::FromRgb(0x3B, 0x86, 0xF7), [Color]::FromRgb(0x1A, 0x5A, 0xD7), 90)
$redBrush = [SolidColorBrush]::new([Color]::FromRgb(0xFF, 0x45, 0x3A))

function New-IconBitmap([hashtable] $Layout) {
    $s = $Layout.Size
    $visual = [DrawingVisual]::new()
    $dc = $visual.RenderOpen()

    # 藍色漸層圓角底板
    $p = $Layout.Plate
    $dc.DrawRoundedRectangle($plateBrush, $null, [Rect]::new($p, $p, $s - 2 * $p, $s - 2 * $p), $Layout.Radius, $Layout.Radius)

    # 白色選取框四角：每個角由一段水平與一段垂直矩形聯集而成
    $t = $Layout.Stroke
    $arm = $Layout.Arm
    $near = $Layout.Box
    $far = $s - $Layout.Box
    $brackets = [GeometryGroup]::new()
    $brackets.FillRule = [FillRule]::Nonzero
    foreach ($x in $near, ($far - $arm)) {
        foreach ($y in $near, ($far - $t)) {
            $brackets.Children.Add([RectangleGeometry]::new([Rect]::new($x, $y, $arm, $t)))
        }
    }
    foreach ($x in $near, ($far - $t)) {
        foreach ($y in $near, ($far - $arm)) {
            $brackets.Children.Add([RectangleGeometry]::new([Rect]::new($x, $y, $t, $arm)))
        }
    }
    $dc.DrawGeometry([Brushes]::White, $null, $brackets)

    # 紅色標註元素：小尺寸為紅點，大尺寸為外框減內框的標註框
    if ($Layout.Dot -gt 0) {
        $dc.DrawEllipse($redBrush, $null, [Point]::new($s / 2, $s / 2), $Layout.Dot / 2, $Layout.Dot / 2)
    }
    else {
        $w, $h, $line, $r = $Layout.Mark
        $x = ($s - $w) / 2
        $y = ($s - $h) / 2
        $inner = [Math]::Max($r - $line, 0)
        $frame = [GeometryGroup]::new()
        $frame.Children.Add([RectangleGeometry]::new([Rect]::new($x, $y, $w, $h), $r, $r))
        $frame.Children.Add([RectangleGeometry]::new([Rect]::new($x + $line, $y + $line, $w - 2 * $line, $h - 2 * $line), $inner, $inner))
        $dc.DrawGeometry($redBrush, $null, $frame)
    }
    $dc.Close()

    $bitmap = [RenderTargetBitmap]::new($s, $s, 96, 96, [PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    # 圖示使用非預乘 alpha
    [FormatConvertedBitmap]::new($bitmap, [PixelFormats]::Bgra32, $null, 0)
}

function ConvertTo-PngBytes([BitmapSource] $Source) {
    $encoder = [PngBitmapEncoder]::new()
    $encoder.Interlace = [PngInterlaceOption]::Off
    $encoder.Frames.Add([BitmapFrame]::Create($Source))
    $stream = [System.IO.MemoryStream]::new()
    $encoder.Save($stream)
    , $stream.ToArray()
}

function ConvertTo-DibBytes([BitmapSource] $Source) {
    $s = $Source.PixelWidth
    $stride = $s * 4
    $pixels = [byte[]]::new($stride * $s)
    $Source.CopyPixels($pixels, $stride, 0)
    $maskStride = (($s + 31) -shr 5) * 4

    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)
    # BITMAPINFOHEADER：高度為色彩層與遮罩層合計的兩倍
    $writer.Write([uint32]40)
    $writer.Write([int32]$s)
    $writer.Write([int32]($s * 2))
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]0)
    $writer.Write([uint32]($pixels.Length + $maskStride * $s))
    $writer.Write([int32]0)
    $writer.Write([int32]0)
    $writer.Write([uint32]0)
    $writer.Write([uint32]0)

    # 色彩層：BGRA，由下往上
    for ($y = $s - 1; $y -ge 0; $y--) {
        $writer.Write($pixels, $y * $stride, $stride)
    }

    # 遮罩層：完全透明的像素設為 1，每列補齊到 32 位元，同樣由下往上
    $mask = [byte[]]::new($maskStride * $s)
    for ($y = 0; $y -lt $s; $y++) {
        $row = ($s - 1 - $y) * $maskStride
        for ($x = 0; $x -lt $s; $x++) {
            if ($pixels[($y * $s + $x) * 4 + 3] -eq 0) {
                $i = $row + ($x -shr 3)
                $mask[$i] = $mask[$i] -bor (0x80 -shr ($x -band 7))
            }
        }
    }
    $writer.Write($mask)
    $writer.Flush()
    , $stream.ToArray()
}

$images = foreach ($layout in $layouts) {
    $bitmap = New-IconBitmap $layout
    $data = if ($layout.Size -ge 256) { ConvertTo-PngBytes $bitmap } else { ConvertTo-DibBytes $bitmap }
    [pscustomobject]@{ Size = $layout.Size; Data = $data }
}

$stream = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($stream)

# ICONDIR
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$images.Count)

# ICONDIRENTRY：寬高 256 以 0 表示
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$image.Data.Length)
    $writer.Write([uint32]$offset)
    $offset += $image.Data.Length
}

foreach ($image in $images) {
    $writer.Write([byte[]]$image.Data)
}
$writer.Flush()

[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($outputFile)) | Out-Null
[System.IO.File]::WriteAllBytes($outputFile, $stream.ToArray())
Get-Item -LiteralPath $outputFile
