#Requires -Version 7
<#
.SYNOPSIS
    煙霧驗證用的已知圖樣視窗：每個像素的顏色由它在視窗內的座標決定，截圖結果可以逐像素比對出任何偏移。

.DESCRIPTION
    以 Per-Monitor V2 建立無邊框的置頂視窗，位置與大小皆為實體像素，圖樣的一個像素對應一個螢幕像素。
    像素編碼：紅 = x 的低 8 位元、綠 = y 的低 8 位元、藍 = x 與 y 的第 8～11 位元（4096×4096 以內每個像素都不同）。
    關閉視窗（WM_CLOSE）即結束。

.PARAMETER X
    視窗左上角的桌面實體像素 X 座標。
#>
param(
    [Parameter(Mandatory)][int]$X,
    [Parameter(Mandatory)][int]$Y,
    [Parameter(Mandatory)][int]$Width,
    [Parameter(Mandatory)][int]$Height,
    [string]$Title = 'ScreenshotApp 煙霧驗證圖樣'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -Language CSharp -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class TestPatternPixels
{
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    // 由上而下、每列 width * 4 位元組的 32 位元 BGRA 像素。
    public static byte[] Create(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                pixels[i] = (byte)((((x >> 8) & 0xF) << 4) | ((y >> 8) & 0xF));
                pixels[i + 1] = (byte)(y & 0xFF);
                pixels[i + 2] = (byte)(x & 0xFF);
                pixels[i + 3] = 0xFF;
            }
        }
        return pixels;
    }
}
'@

[void][TestPatternPixels]::SetThreadDpiAwarenessContext([IntPtr]-4)

$bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppRgb)
$data = $bitmap.LockBits([System.Drawing.Rectangle]::new(0, 0, $Width, $Height), [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $bitmap.PixelFormat)
try {
    $pixels = [TestPatternPixels]::Create($Width, $Height)
    [System.Runtime.InteropServices.Marshal]::Copy($pixels, 0, $data.Scan0, $pixels.Length)
}
finally {
    $bitmap.UnlockBits($data)
}

$bounds = [System.Drawing.Rectangle]::new($X, $Y, $Width, $Height)
$form = [System.Windows.Forms.Form]::new()
$form.Text = $Title
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
$form.AutoScaleMode = [System.Windows.Forms.AutoScaleMode]::None
$form.TopMost = $true
$form.Bounds = $bounds
$form.Add_Load({ $form.Bounds = $bounds })
$form.Add_Paint({
        param($sender, $e)
        # 一比一複製像素，不做任何內插。
        $e.Graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $e.Graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $e.Graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $e.Graphics.DrawImage($bitmap, [System.Drawing.Rectangle]::new(0, 0, $Width, $Height), 0, 0, $Width, $Height, [System.Drawing.GraphicsUnit]::Pixel)
    })

[System.Windows.Forms.Application]::Run($form)
$bitmap.Dispose()
