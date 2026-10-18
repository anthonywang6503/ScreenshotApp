using System.Windows;
using System.Windows.Media;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Editor.Rendering;

namespace ScreenshotApp.Editor;

/// <summary>
/// 縮放容器內的標註層，區域單位即圖片像素：以 <see cref="AnnotationPainter"/> 繪製畫面上的標註，
/// 裁切工具中把待套用範圍以外暗化。
/// </summary>
internal sealed class AnnotationLayer : FrameworkElement
{
    private static readonly Brush CropShade = CreateFrozenBrush(Color.FromArgb(0x99, 0x00, 0x00, 0x00));

    private EditorSession? _session;

    public EditorSession? Session
    {
        get => _session;
        set
        {
            _session = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_session is null)
        {
            return;
        }

        foreach (var annotation in _session.VisibleAnnotations)
        {
            AnnotationPainter.Paint(drawingContext, annotation);
        }

        if (_session.PendingCrop is { } crop)
        {
            var shade = new CombinedGeometry(
                GeometryCombineMode.Exclude, new RectangleGeometry(ToRect(_session.Document.Bounds)), new RectangleGeometry(ToRect(crop)));
            drawingContext.DrawGeometry(CropShade, null, shade);
        }
    }

    private static Rect ToRect(PixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
