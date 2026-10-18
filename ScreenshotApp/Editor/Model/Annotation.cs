using System.Windows;
using System.Windows.Media;

namespace ScreenshotApp.Editor.Model;

/// <summary>控制點：矩形範圍的四角與四邊中點，或線段的兩端。</summary>
internal enum HandleKind
{
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
    Start,
    End,
}

/// <summary>控制點與其位置（圖片像素座標，可為像素格線或像素中心）。</summary>
internal readonly record struct AnnotationHandle(HandleKind Kind, Point Position);

/// <summary>
/// 不可變的標註，幾何為整數圖片像素、以原始圖片左上角為原點，裁切時不改寫。
/// 修改標註就是產生一份 Id 相同的新 record；文件依 Id 辨識同一個標註。
/// </summary>
internal abstract record Annotation
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Color Color { get; init; }

    /// <summary>繪製範圍（含線寬），選取外框依此繪製。</summary>
    public abstract Rect Bounds { get; }

    /// <summary>可拖曳調整的控制點；沒有時為空。</summary>
    public virtual IReadOnlyList<AnnotationHandle> Handles => [];

    /// <summary>點（圖片像素座標）是否落在標註的可見部分上；<paramref name="tolerance"/> 為額外容許的距離（圖片像素）。</summary>
    public abstract bool HitTest(Point point, double tolerance);

    public abstract Annotation Translate(int dx, int dy);

    /// <summary>把控制點拖曳 (<paramref name="dx"/>, <paramref name="dy"/>) 個像素後的標註；沒有該控制點時不變。</summary>
    public virtual Annotation Resize(HandleKind handle, int dx, int dy) => this;
}

/// <summary>
/// 以矩形範圍描述的標註（矩形框、橢圓）。範圍為半開區間，筆劃畫在範圍內側；
/// 範圍寬或高不超過兩倍線寬時，內側沒有空心部分，整塊為實心。
/// </summary>
internal abstract record BoxAnnotation : Annotation
{
    public required PixelRect Rect { get; init; }

    public required int StrokeWidth { get; init; }

    public override Rect Bounds => new(Rect.X, Rect.Y, Rect.Width, Rect.Height);

    public override IReadOnlyList<AnnotationHandle> Handles => BoxHandles.Of(Rect);

    /// <summary>內側空心部分是否存在。</summary>
    public bool IsHollow => Rect.Width > 2 * StrokeWidth && Rect.Height > 2 * StrokeWidth;

    public override Annotation Translate(int dx, int dy) => this with { Rect = Rect.Offset(dx, dy) };

    public override Annotation Resize(HandleKind handle, int dx, int dy) =>
        this with { Rect = BoxHandles.Resize(Rect, handle, dx, dy) };
}

/// <summary>矩形框；只有筆劃是可見部分，點在空心內側不算命中，才能點到框內較底層的標註。</summary>
internal sealed record RectangleAnnotation : BoxAnnotation
{
    public override bool HitTest(Point point, double tolerance)
    {
        var outer = Bounds;
        outer.Inflate(tolerance, tolerance);
        if (!outer.Contains(point))
        {
            return false;
        }

        var hollowWidth = Rect.Width - 2 * (StrokeWidth + tolerance);
        var hollowHeight = Rect.Height - 2 * (StrokeWidth + tolerance);
        if (!IsHollow || hollowWidth <= 0 || hollowHeight <= 0)
        {
            return true;
        }

        var hollow = new Rect(Rect.X + StrokeWidth + tolerance, Rect.Y + StrokeWidth + tolerance, hollowWidth, hollowHeight);
        return !hollow.Contains(point);
    }
}

/// <summary>橢圓；與矩形框相同，只有筆劃是可見部分。</summary>
internal sealed record EllipseAnnotation : BoxAnnotation
{
    public Point Center => new(Rect.X + Rect.Width / 2.0, Rect.Y + Rect.Height / 2.0);

    public override bool HitTest(Point point, double tolerance)
    {
        var offset = point - Center;
        var distance = offset.Length;
        var outerA = Rect.Width / 2.0;
        var outerB = Rect.Height / 2.0;
        var innerA = outerA - StrokeWidth;
        var innerB = outerB - StrokeWidth;
        if (distance < 1e-9)
        {
            return !IsHollow || Math.Min(innerA, innerB) <= tolerance;
        }

        // 沿著中心到該點的射線比較：點落在內外兩個橢圓邊界之間（各放寬容許距離）即命中。
        var direction = offset / distance;
        var outerEdge = RadiusAlong(direction, outerA, outerB);
        if (distance > outerEdge + tolerance)
        {
            return false;
        }

        return !IsHollow || distance >= RadiusAlong(direction, innerA, innerB) - tolerance;
    }

    // 中心沿單位向量方向到半軸為 a、b 的橢圓邊界的距離。
    private static double RadiusAlong(Vector direction, double a, double b) =>
        1 / Math.Sqrt(direction.X * direction.X / (a * a) + direction.Y * direction.Y / (b * b));
}

/// <summary>以起點與終點像素描述的標註（直線、箭頭），繪製時通過兩個像素的中心。</summary>
internal abstract record SegmentAnnotation : Annotation
{
    public required PixelPoint Start { get; init; }

    public required PixelPoint End { get; init; }

    public required int StrokeWidth { get; init; }

    public Point StartPoint => PixelCenter(Start);

    public Point EndPoint => PixelCenter(End);

    public override Rect Bounds
    {
        get
        {
            var bounds = new Rect(StartPoint, EndPoint);
            bounds.Inflate(StrokeWidth / 2.0, StrokeWidth / 2.0);
            return bounds;
        }
    }

    public override IReadOnlyList<AnnotationHandle> Handles =>
        [new(HandleKind.Start, StartPoint), new(HandleKind.End, EndPoint)];

    public override bool HitTest(Point point, double tolerance) =>
        Geometry2D.DistanceToSegment(point, StartPoint, EndPoint) <= StrokeWidth / 2.0 + tolerance;

    public override Annotation Translate(int dx, int dy) =>
        this with { Start = new PixelPoint(Start.X + dx, Start.Y + dy), End = new PixelPoint(End.X + dx, End.Y + dy) };

    public override Annotation Resize(HandleKind handle, int dx, int dy) => handle switch
    {
        HandleKind.Start => this with { Start = new PixelPoint(Start.X + dx, Start.Y + dy) },
        HandleKind.End => this with { End = new PixelPoint(End.X + dx, End.Y + dy) },
        _ => this,
    };

    protected static Point PixelCenter(PixelPoint pixel) => new(pixel.X + 0.5, pixel.Y + 0.5);
}

internal sealed record LineAnnotation : SegmentAnnotation;

/// <summary>箭頭；頭部位於終點，大小隨線寬增加，線段比頭部短時等比例縮小。</summary>
internal sealed record ArrowAnnotation : SegmentAnnotation
{
    private const double HeadLengthPerStroke = 4;
    private const double MinHeadLength = 12;

    /// <summary>頭部的三個頂點與線身終點；起訖相同時為 null。</summary>
    public ArrowHead? Head
    {
        get
        {
            var shaft = EndPoint - StartPoint;
            var length = shaft.Length;
            if (length < 1e-9)
            {
                return null;
            }

            var direction = shaft / length;
            var headLength = Math.Min(length, Math.Max(MinHeadLength, StrokeWidth * HeadLengthPerStroke));
            var normal = new Vector(-direction.Y, direction.X) * (headLength / 2);
            var baseCenter = EndPoint - direction * headLength;

            // 線身停在頭部中段：該處頭部寬度不小於線寬，平頭線帽不會從三角形兩側露出。
            return new ArrowHead(EndPoint, baseCenter + normal, baseCenter - normal, EndPoint - direction * (headLength / 2));
        }
    }

    public override Rect Bounds
    {
        get
        {
            var bounds = base.Bounds;
            if (Head is { } head)
            {
                bounds.Union(head.Left);
                bounds.Union(head.Right);
            }

            return bounds;
        }
    }

    public override bool HitTest(Point point, double tolerance) =>
        base.HitTest(point, tolerance)
        || (Head is { } head && Geometry2D.DistanceToTriangle(point, head.Tip, head.Left, head.Right) <= tolerance);
}

/// <summary>箭頭頭部：尖端、底邊兩端，以及線身終止的位置。</summary>
internal readonly record struct ArrowHead(Point Tip, Point Left, Point Right, Point ShaftEnd);

/// <summary>文字；起點為文字版面左上角，只依明確的換行分行。字級為圖片像素，不可調整控制點。</summary>
internal sealed record TextAnnotation : Annotation
{
    public required PixelPoint Origin { get; init; }

    public required string Text { get; init; }

    public required string FontFamily { get; init; }

    public required int FontSize { get; init; }

    public override Rect Bounds
    {
        get
        {
            var layout = TextLayout.Create(Text, FontFamily, FontSize, Brushes.Black);
            return new Rect(Origin.X, Origin.Y, layout.WidthIncludingTrailingWhitespace, layout.Height);
        }
    }

    public override bool HitTest(Point point, double tolerance)
    {
        var bounds = Bounds;
        bounds.Inflate(tolerance, tolerance);
        return bounds.Contains(point);
    }

    public override Annotation Translate(int dx, int dy) => this with { Origin = new PixelPoint(Origin.X + dx, Origin.Y + dy) };
}

/// <summary>步驟編號：以中心像素為圓心的實心圓與編號。拖曳外接正方形的四角調整半徑。</summary>
internal sealed record StepNumberAnnotation : Annotation
{
    public const int MinRadius = 6;
    public const int MaxRadius = 500;

    public required PixelPoint Center { get; init; }

    public required int Radius { get; init; }

    public required int Number { get; init; }

    public Point CenterPoint => new(Center.X + 0.5, Center.Y + 0.5);

    public override Rect Bounds => new(CenterPoint.X - Radius, CenterPoint.Y - Radius, 2 * Radius, 2 * Radius);

    public override IReadOnlyList<AnnotationHandle> Handles
    {
        get
        {
            var bounds = Bounds;
            return
            [
                new(HandleKind.TopLeft, bounds.TopLeft),
                new(HandleKind.TopRight, bounds.TopRight),
                new(HandleKind.BottomRight, bounds.BottomRight),
                new(HandleKind.BottomLeft, bounds.BottomLeft),
            ];
        }
    }

    /// <summary>依文字字級決定的預設半徑，讓編號與同樣字級的文字搭配。</summary>
    public static int DefaultRadius(int fontSize) =>
        Math.Clamp((int)Math.Round(fontSize * 0.75, MidpointRounding.AwayFromZero), MinRadius, MaxRadius);

    public override bool HitTest(Point point, double tolerance) => (point - CenterPoint).Length <= Radius + tolerance;

    public override Annotation Translate(int dx, int dy) => this with { Center = new PixelPoint(Center.X + dx, Center.Y + dy) };

    public override Annotation Resize(HandleKind handle, int dx, int dy)
    {
        // 角落往外拖（遠離圓心的方向）時放大；取兩軸位移的平均，對角線拖曳最直覺。
        (int sx, int sy) = handle switch
        {
            HandleKind.TopLeft => (-1, -1),
            HandleKind.TopRight => (1, -1),
            HandleKind.BottomRight => (1, 1),
            HandleKind.BottomLeft => (-1, 1),
            _ => (0, 0),
        };
        if (sx == 0)
        {
            return this;
        }

        var growth = (int)Math.Round((sx * dx + sy * dy) / 2.0, MidpointRounding.AwayFromZero);
        return this with { Radius = Math.Clamp(Radius + growth, MinRadius, MaxRadius) };
    }
}
