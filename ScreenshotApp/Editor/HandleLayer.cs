using System.Windows;
using System.Windows.Media;
using ScreenshotApp.Capture;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Editor;

/// <summary>
/// 縮放容器外的控制點層：選取外框、控制點、裁切範圍與文字輸入框外框。以 DIP 繪製，在任何縮放下都是固定的螢幕尺寸；
/// 位置由 <see cref="CoordinateMapper"/> 依目前的畫布轉換換算，線條對齊實體像素以保持清晰。
/// </summary>
internal sealed class HandleLayer : FrameworkElement
{
    /// <summary>控制點方塊的邊長（DIP）。</summary>
    public const double HandleSize = 8;

    // 選取外框與標註之間留下的間隙（DIP），外框不會蓋住標註的邊緣。
    private const double FrameGap = 2;

    private static readonly Brush Accent = CreateFrozenBrush(Color.FromRgb(0x00, 0x78, 0xD7));

    public EditorSession? Session { get; set; }

    /// <summary>取得目前的畫布轉換；畫布尚未完成版面時為 null。</summary>
    public Func<ImageViewTransform?>? TransformProvider { get; set; }

    /// <summary>文字輸入框的範圍（圖片像素）；沒有在輸入時為 null。</summary>
    public Rect? TextEditBounds { get; set; }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (Session is not { } session || TransformProvider?.Invoke() is not { } view)
        {
            return;
        }

        var devicePixel = 1 / VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (session.PendingCrop is { } crop)
        {
            DrawFrame(drawingContext, ToView(new Rect(crop.X, crop.Y, crop.Width, crop.Height), view), devicePixel, dashed: false);
            DrawHandles(drawingContext, BoxHandles.Of(crop), view, devicePixel);
        }
        else if (session.TextEdit is null && session.SelectionDisplay is { } selected)
        {
            if (selected is not SegmentAnnotation)
            {
                var frame = ToView(selected.Bounds, view);
                frame.Inflate(FrameGap, FrameGap);
                DrawFrame(drawingContext, frame, devicePixel, dashed: true);
            }

            DrawHandles(drawingContext, selected.Handles, view, devicePixel);
        }

        if (TextEditBounds is { } text)
        {
            var frame = ToView(text, view);
            frame.Inflate(FrameGap, FrameGap);
            DrawFrame(drawingContext, frame, devicePixel, dashed: true);
        }
    }

    private static Rect ToView(Rect image, ImageViewTransform view) =>
        new(CoordinateMapper.ImageToViewDip(image.TopLeft, view), CoordinateMapper.ImageToViewDip(image.BottomRight, view));

    // 一個實體像素寬的外框，邊落在實體像素中心，不會模糊成兩個像素。
    private static void DrawFrame(DrawingContext context, Rect frame, double devicePixel, bool dashed)
    {
        var pen = new Pen(Accent, devicePixel);
        if (dashed)
        {
            pen.DashStyle = new DashStyle([4, 3], 0);
        }

        pen.Freeze();
        var topLeft = SnapToPixelCenter(frame.TopLeft, devicePixel);
        var bottomRight = SnapToPixelCenter(frame.BottomRight, devicePixel);
        context.DrawRectangle(null, pen, new Rect(topLeft, bottomRight));
    }

    private static void DrawHandles(DrawingContext context, IReadOnlyList<AnnotationHandle> handles, ImageViewTransform view, double devicePixel)
    {
        var pen = new Pen(Accent, devicePixel);
        pen.Freeze();
        var half = Math.Round(HandleSize / 2 / devicePixel) * devicePixel;
        foreach (var handle in handles)
        {
            var center = SnapToPixelCenter(CoordinateMapper.ImageToViewDip(handle.Position, view), devicePixel);
            context.DrawRectangle(Brushes.White, pen, new Rect(center.X - half, center.Y - half, 2 * half, 2 * half));
        }
    }

    private static Point SnapToPixelCenter(Point point, double devicePixel) =>
        new((Math.Floor(point.X / devicePixel) + 0.5) * devicePixel, (Math.Floor(point.Y / devicePixel) + 0.5) * devicePixel);

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
