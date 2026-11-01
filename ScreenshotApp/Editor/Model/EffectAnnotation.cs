using System.Windows;

namespace ScreenshotApp.Editor.Model;

/// <summary>以原圖像素產生的不透明矩形效果；顏色不參與繪製。</summary>
internal abstract record EffectAnnotation : Annotation
{
    public required PixelRect Rect { get; init; }

    public required int Strength { get; init; }

    public override Rect Bounds => new(Rect.X, Rect.Y, Rect.Width, Rect.Height);

    public override IReadOnlyList<AnnotationHandle> Handles => BoxHandles.Of(Rect);

    public override bool HitTest(Point point, double tolerance) =>
        !Rect.IsEmpty && point.X >= Rect.X - tolerance && point.X <= Rect.Right + tolerance &&
        point.Y >= Rect.Y - tolerance && point.Y <= Rect.Bottom + tolerance;

    public override Annotation Translate(int dx, int dy) => this with { Rect = Rect.Offset(dx, dy) };

    public override Annotation Resize(HandleKind handle, int dx, int dy) =>
        this with { Rect = BoxHandles.Resize(Rect, handle, dx, dy) };
}

internal sealed record MosaicAnnotation : EffectAnnotation
{
    public const int MinimumStrength = 2;
    public const int MaximumStrength = 64;
    public const int DefaultStrength = 12;
}

internal sealed record BlurAnnotation : EffectAnnotation
{
    public const int MinimumStrength = 1;
    public const int MaximumStrength = 32;
    public const int DefaultStrength = 6;
}
