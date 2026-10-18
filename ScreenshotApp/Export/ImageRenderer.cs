using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Editor.Rendering;

namespace ScreenshotApp.Export;

/// <summary>
/// PNG 與剪貼簿共用的唯一圖片合成器：以原始圖片尺寸合成底圖與全部標註，再取出裁切範圍。
/// 輸出寬高嚴格等於裁切範圍，與編輯畫布的縮放無關。需在 UI（STA）執行緒呼叫；結果已凍結，可交給其他執行緒使用。
/// </summary>
internal static class ImageRenderer
{
    public static BitmapSource Render(ImageDocument document)
    {
        var visual = new DrawingVisual();

        // 底圖以原始像素尺寸繪製，最近鄰取樣確保輸出像素與原圖完全相同；文字與畫布相同使用灰階平滑。
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);
        using (var context = visual.RenderOpen())
        {
            // 以像素尺寸指定繪製範圍，不使用圖檔內記錄的 DPI。
            context.DrawImage(document.Source, new Rect(0, 0, document.PixelWidth, document.PixelHeight));
            foreach (var annotation in document.Annotations)
            {
                AnnotationPainter.Paint(context, annotation);
            }
        }

        // 一律合成整張圖片再取出裁切範圍：平滑像素（例如小字級文字）會隨繪製位置改變，平移到裁切原點再繪製時，
        // 範圍內的內容可能與未裁切時差幾個像素。裁切只應去掉範圍外的像素。
        var full = new RenderTargetBitmap(document.PixelWidth, document.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        full.Render(visual);
        var crop = document.CropRect;
        if (crop == document.Bounds)
        {
            full.Freeze();
            return full;
        }

        var stride = crop.Width * 4;
        var pixels = new byte[stride * crop.Height];
        full.CopyPixels(new Int32Rect(crop.X, crop.Y, crop.Width, crop.Height), pixels, stride, 0);
        var output = BitmapSource.Create(crop.Width, crop.Height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        output.Freeze();
        return output;
    }
}
