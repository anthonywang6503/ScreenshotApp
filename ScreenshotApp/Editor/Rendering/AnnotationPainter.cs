using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Editor.Rendering;

/// <summary>
/// 標註的唯一繪製碼，以圖片像素座標繪製。編輯畫布（縮放容器之內）與匯出（96 DPI 點陣圖）共用同一份程式，
/// 繪製碼不知道螢幕 DPI 或畫布縮放比例，因此畫面、PNG 與剪貼簿的圖形一致。
/// </summary>
internal static class AnnotationPainter
{
    private static readonly Typeface StepNumberTypeface =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public static void Paint(DrawingContext context, Annotation annotation)
    {
        var brush = new SolidColorBrush(annotation.Color);
        brush.Freeze();
        switch (annotation)
        {
            case RectangleAnnotation rectangle:
                PaintRectangle(context, rectangle, brush);
                break;
            case EllipseAnnotation ellipse:
                PaintEllipse(context, ellipse, brush);
                break;
            case ArrowAnnotation arrow:
                PaintArrow(context, arrow, brush);
                break;
            case LineAnnotation line:
                PaintLine(context, line, brush);
                break;
            case TextAnnotation text:
                context.DrawText(TextLayout.Create(text.Text, text.FontFamily, text.FontSize, brush), new Point(text.Origin.X, text.Origin.Y));
                break;
            case StepNumberAnnotation step:
                PaintStepNumber(context, step, brush);
                break;
            default:
                throw new ArgumentException($"不支援的標註種類：{annotation.GetType().Name}", nameof(annotation));
        }
    }

    // 筆劃畫在範圍內側：以線寬一半內縮後描邊，水平與垂直邊恰好落在像素格線上。
    private static void PaintRectangle(DrawingContext context, RectangleAnnotation rectangle, Brush brush)
    {
        var bounds = rectangle.Bounds;
        if (!rectangle.IsHollow)
        {
            context.DrawRectangle(brush, null, bounds);
            return;
        }

        var stroke = rectangle.StrokeWidth;
        var path = new Rect(bounds.X + stroke / 2.0, bounds.Y + stroke / 2.0, bounds.Width - stroke, bounds.Height - stroke);
        context.DrawRectangle(null, CreatePen(brush, stroke, PenLineCap.Flat, PenLineCap.Flat), path);
    }

    private static void PaintEllipse(DrawingContext context, EllipseAnnotation ellipse, Brush brush)
    {
        var rect = ellipse.Rect;
        if (!ellipse.IsHollow)
        {
            context.DrawEllipse(brush, null, ellipse.Center, rect.Width / 2.0, rect.Height / 2.0);
            return;
        }

        var stroke = ellipse.StrokeWidth;
        context.DrawEllipse(
            null, CreatePen(brush, stroke, PenLineCap.Flat, PenLineCap.Flat), ellipse.Center, (rect.Width - stroke) / 2.0, (rect.Height - stroke) / 2.0);
    }

    private static void PaintLine(DrawingContext context, LineAnnotation line, Brush brush)
    {
        if (line.Start == line.End)
        {
            PaintDot(context, line, brush);
            return;
        }

        context.DrawLine(CreatePen(brush, line.StrokeWidth, PenLineCap.Round, PenLineCap.Round), line.StartPoint, line.EndPoint);
    }

    private static void PaintArrow(DrawingContext context, ArrowAnnotation arrow, Brush brush)
    {
        if (arrow.Head is not { } head)
        {
            PaintDot(context, arrow, brush);
            return;
        }

        // 線身以平頭線帽終止在頭部之內，尖端不會被線帽撐圓。
        context.DrawLine(CreatePen(brush, arrow.StrokeWidth, PenLineCap.Round, PenLineCap.Flat), arrow.StartPoint, head.ShaftEnd);
        var triangle = new StreamGeometry();
        using (var figure = triangle.Open())
        {
            figure.BeginFigure(head.Tip, isFilled: true, isClosed: true);
            figure.LineTo(head.Left, isStroked: false, isSmoothJoin: false);
            figure.LineTo(head.Right, isStroked: false, isSmoothJoin: false);
        }

        triangle.Freeze();
        context.DrawGeometry(brush, null, triangle);
    }

    // 起點與終點相同的線段（例如把一端拖到另一端上）畫成線寬大小的圓點，不會消失。
    private static void PaintDot(DrawingContext context, SegmentAnnotation segment, Brush brush) =>
        context.DrawEllipse(brush, null, segment.StartPoint, segment.StrokeWidth / 2.0, segment.StrokeWidth / 2.0);

    private static void PaintStepNumber(DrawingContext context, StepNumberAnnotation step, Brush brush)
    {
        var center = step.CenterPoint;
        context.DrawEllipse(brush, null, center, step.Radius, step.Radius);

        // 多位數時縮小字級，讓編號留在圓內；以文字外框幾何的中心置中，數字不會因字型的上下留白而偏移。
        var textBrush = IsLight(step.Color) ? Brushes.Black : Brushes.White;
        var text = step.Number.ToString(CultureInfo.InvariantCulture);
        var fontSize = step.Radius * 1.2;
        var maxWidth = step.Radius * 1.6;
        var layout = CreateStepNumberText(text, fontSize, textBrush);
        if (layout.Width > maxWidth)
        {
            layout = CreateStepNumberText(text, fontSize * maxWidth / layout.Width, textBrush);
        }

        var glyphs = layout.BuildGeometry(new Point(0, 0));
        var ink = glyphs.Bounds;
        if (ink.IsEmpty)
        {
            return;
        }

        var offset = new TranslateTransform(center.X - (ink.X + ink.Width / 2), center.Y - (ink.Y + ink.Height / 2));
        offset.Freeze();
        context.PushTransform(offset);
        context.DrawGeometry(textBrush, null, glyphs);
        context.Pop();
    }

    private static FormattedText CreateStepNumberText(string text, double fontSize, Brush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, StepNumberTypeface, fontSize, brush, 1.0);

    // 亮色底（橙、黃、白等）配黑字，其餘配白字；以人眼感受的亮度判斷，橙色白字的對比太低。
    private static bool IsLight(Color color) => 0.299 * color.R + 0.587 * color.G + 0.114 * color.B > 150;

    private static Pen CreatePen(Brush brush, double thickness, PenLineCap startCap, PenLineCap endCap)
    {
        var pen = new Pen(brush, thickness) { StartLineCap = startCap, EndLineCap = endCap, LineJoin = PenLineJoin.Miter };
        pen.Freeze();
        return pen;
    }
}
