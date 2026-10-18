using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Editor.Rendering;

namespace ScreenshotApp.Export;

/// <summary>
/// PNG 與剪貼簿共用的唯一圖片合成器：以原始圖片座標繪製底圖與全部標註，再依裁切範圍平移輸出。
/// 輸出寬高嚴格等於裁切範圍，與編輯畫布的縮放無關。需在 UI（STA）執行緒呼叫；結果已凍結，可交給其他執行緒使用。
/// </summary>
internal static class ImageRenderer
{
    public static BitmapSource Render(ImageDocument document)
    {
        var crop = document.CropRect;
        var visual = new DrawingVisual();

        // 底圖以原始像素尺寸、整數平移繪製，最近鄰取樣確保輸出像素與原圖完全相同；文字與畫布相同使用灰階平滑。
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);
        using (var context = visual.RenderOpen())
        {
            var offset = new TranslateTransform(-crop.X, -crop.Y);
            offset.Freeze();
            context.PushTransform(offset);

            // 以像素尺寸指定繪製範圍，不使用圖檔內記錄的 DPI。
            context.DrawImage(document.Source, new Rect(0, 0, document.PixelWidth, document.PixelHeight));
            foreach (var annotation in document.Annotations)
            {
                AnnotationPainter.Paint(context, annotation);
            }

            context.Pop();
        }

        var bitmap = new RenderTargetBitmap(crop.Width, crop.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
