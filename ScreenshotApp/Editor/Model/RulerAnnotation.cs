using System.Windows;
using System.Windows.Media;
using ScreenshotApp.Resources;

namespace ScreenshotApp.Editor.Model;

internal sealed record RulerAnnotation : SegmentAnnotation
{
    public required PixelRect ImageBounds { get; init; }
    public PixelMeasurement Measurement => new(Start, End);
    public string Label => Strings.Format("Ruler_Length", Measurement.DisplayLength);
    public double FontSize => Math.Max(12, StrokeWidth * 4);
    public Vector TickOffset
    {
        get
        {
            var direction = EndPoint - StartPoint;
            return direction.Length == 0 ? new Vector(0, Math.Max(6, StrokeWidth * 3)) :
                new Vector(-direction.Y, direction.X) / direction.Length * Math.Max(6, StrokeWidth * 3);
        }
    }

    public Size LabelSize
    {
        get
        {
            var text = TextLayout.Create(Label, "Segoe UI", FontSize, Brushes.Black);
            return new Size(text.Width + 8, text.Height + 4);
        }
    }

    public Rect LabelBounds
    {
        get
        {
            var size = LabelSize;
            var scale = Math.Min(1, Math.Min(ImageBounds.Width / size.Width, ImageBounds.Height / size.Height));
            var width = size.Width * scale;
            var height = size.Height * scale;
            return new Rect(
                Math.Clamp((StartPoint.X + EndPoint.X - width) / 2, ImageBounds.X, ImageBounds.Right - width),
                Math.Clamp((StartPoint.Y + EndPoint.Y - height) / 2, ImageBounds.Y, ImageBounds.Bottom - height), width, height);
        }
    }

    public override Rect Bounds
    {
        get
        {
            var bounds = base.Bounds;
            bounds.Union(new Rect(StartPoint - TickOffset, StartPoint + TickOffset));
            bounds.Union(new Rect(EndPoint - TickOffset, EndPoint + TickOffset));
            bounds.Inflate(StrokeWidth / 2.0, StrokeWidth / 2.0);
            bounds.Union(LabelBounds);
            return bounds;
        }
    }

    public override bool HitTest(Point point, double tolerance)
    {
        var label = LabelBounds;
        label.Inflate(tolerance, tolerance);
        return label.Contains(point) || base.HitTest(point, tolerance)
            || Geometry2D.DistanceToSegment(point, StartPoint - TickOffset, StartPoint + TickOffset) <= StrokeWidth / 2.0 + tolerance
            || Geometry2D.DistanceToSegment(point, EndPoint - TickOffset, EndPoint + TickOffset) <= StrokeWidth / 2.0 + tolerance;
    }
}
