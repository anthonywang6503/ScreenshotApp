using System.Globalization;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Settings;

namespace ScreenshotApp.History;

internal sealed record HistorySnapshot(BitmapSource Source, Annotation[] Annotations, PixelRect Crop, DateTimeOffset Created)
{
    public static HistorySnapshot Capture(ImageDocument document, DateTimeOffset created) =>
        new(document.Source, document.Annotations.ToArray(), document.CropRect, created);

    public ImageDocument Restore() => new(Source, Annotations, Crop);
}

/// <summary>只保存資料欄位，避免序列化標註的計算屬性與 WPF 版面物件。</summary>
internal static class HistoryJson
{
    private sealed record DocumentData(int Version, DateTimeOffset Created, int Width, int Height, int[] Crop, AnnotationData[] Annotations);
    private sealed record AnnotationData(string Kind, Guid Id, string Color, int[] Geometry, int Size,
        string? Text = null, string? Font = null, int? Number = null);

    public static string Serialize(HistorySnapshot snapshot) => JsonSerializer.Serialize(new DocumentData(
        1, snapshot.Created, snapshot.Source.PixelWidth, snapshot.Source.PixelHeight, RectData(snapshot.Crop),
        snapshot.Annotations.Select(ToData).ToArray()));

    public static HistorySnapshot? Deserialize(string json, BitmapSource source)
    {
        try
        {
            var data = JsonSerializer.Deserialize<DocumentData>(json);
            if (data is null || data.Version != 1 || data.Created == default || data.Width != source.PixelWidth
                || data.Height != source.PixelHeight || data.Annotations is null) return null;
            var bounds = new PixelRect(0, 0, source.PixelWidth, source.PixelHeight);
            var crop = ReadRect(data.Crop);
            if (!bounds.Contains(crop)) return null;
            var annotations = data.Annotations.Select(item => FromData(item, bounds)).ToArray();
            if (annotations.Select(a => a.Id).Distinct().Count() != annotations.Length) return null;
            return new HistorySnapshot(source, annotations, crop, data.Created);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or OverflowException or FormatException)
        {
            return null;
        }
    }

    private static int[] RectData(PixelRect rect) => [rect.X, rect.Y, rect.Width, rect.Height];

    private static AnnotationData ToData(Annotation a)
    {
        var color = $"{a.Color.A:X2}{a.Color.R:X2}{a.Color.G:X2}{a.Color.B:X2}";
        return a switch
        {
            BoxAnnotation box => new(a is RectangleAnnotation ? "rectangle" : "ellipse", a.Id, color, RectData(box.Rect), box.StrokeWidth),
            SegmentAnnotation line => new(a is RulerAnnotation ? "ruler" : a is ArrowAnnotation ? "arrow" : "line",
                a.Id, color, [line.Start.X, line.Start.Y, line.End.X, line.End.Y], line.StrokeWidth),
            EffectAnnotation effect => new(a is MosaicAnnotation ? "mosaic" : "blur", a.Id, color, RectData(effect.Rect), effect.Strength),
            TextAnnotation text => new("text", a.Id, color, [text.Origin.X, text.Origin.Y], text.FontSize, text.Text, text.FontFamily),
            StepNumberAnnotation step => new("step", a.Id, color, [step.Center.X, step.Center.Y], step.Radius, Number: step.Number),
            _ => throw new ArgumentException("不支援的標註種類。"),
        };
    }

    private static Annotation FromData(AnnotationData? data, PixelRect bounds)
    {
        if (data is null || data.Id == Guid.Empty || data.Color is null || data.Color.Length != 8
            || !uint.TryParse(data.Color, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb)
            || data.Geometry is null || data.Geometry.Length is not (2 or 4)
            || data.Geometry.Any(n => n < -int.MaxValue / 4 || n > int.MaxValue / 4))
            throw new ArgumentException("標註資料無效。");
        var color = Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        var g = data.Geometry;
        var start = new PixelPoint(g[0], g[1]);
        var end = g.Length == 4 ? new PixelPoint(g[2], g[3]) : default;
        Annotation annotation;
        switch (data.Kind)
        {
            case "rectangle":
            case "ellipse":
                CheckSize(data.Size, AppSettings.MinStrokeWidth, AppSettings.MaxStrokeWidth);
                var rect = ReadRect(g);
                annotation = data.Kind == "rectangle"
                    ? new RectangleAnnotation { Rect = rect, StrokeWidth = data.Size, Color = color }
                    : new EllipseAnnotation { Rect = rect, StrokeWidth = data.Size, Color = color };
                break;
            case "line":
            case "arrow":
            case "ruler":
                CheckSize(data.Size, AppSettings.MinStrokeWidth, AppSettings.MaxStrokeWidth);
                if (g.Length != 4) throw new ArgumentException("線段資料無效。");
                annotation = data.Kind switch
                {
                    "ruler" => new RulerAnnotation { Start = start, End = end, StrokeWidth = data.Size, Color = color, ImageBounds = bounds },
                    "arrow" => new ArrowAnnotation { Start = start, End = end, StrokeWidth = data.Size, Color = color },
                    _ => new LineAnnotation { Start = start, End = end, StrokeWidth = data.Size, Color = color },
                };
                break;
            case "mosaic":
                CheckSize(data.Size, MosaicAnnotation.MinimumStrength, MosaicAnnotation.MaximumStrength);
                annotation = new MosaicAnnotation { Rect = ReadRect(g), Strength = data.Size, Color = color };
                break;
            case "blur":
                CheckSize(data.Size, BlurAnnotation.MinimumStrength, BlurAnnotation.MaximumStrength);
                annotation = new BlurAnnotation { Rect = ReadRect(g), Strength = data.Size, Color = color };
                break;
            case "text":
                CheckSize(data.Size, AppSettings.MinFontSize, AppSettings.MaxFontSize);
                if (g.Length != 2 || data.Text is null || string.IsNullOrWhiteSpace(data.Font)) throw new ArgumentException("文字資料無效。");
                annotation = new TextAnnotation { Origin = start, Text = data.Text, FontFamily = data.Font, FontSize = data.Size, Color = color };
                break;
            case "step":
                CheckSize(data.Size, StepNumberAnnotation.MinRadius, StepNumberAnnotation.MaxRadius);
                if (g.Length != 2 || data.Number is null or < 1 or int.MaxValue) throw new ArgumentException("編號資料無效。");
                annotation = new StepNumberAnnotation { Center = start, Radius = data.Size, Number = data.Number.Value, Color = color };
                break;
            default: throw new ArgumentException("不支援的標註種類。");
        }
        return annotation with { Id = data.Id };
    }

    private static PixelRect ReadRect(int[]? values)
    {
        if (values is not { Length: 4 } || values[2] <= 0 || values[3] <= 0) throw new ArgumentException("矩形資料無效。");
        _ = checked(values[0] + values[2]);
        _ = checked(values[1] + values[3]);
        return new PixelRect(values[0], values[1], values[2], values[3]);
    }

    private static void CheckSize(int value, int min, int max)
    {
        if (value < min || value > max) throw new ArgumentException("樣式數值無效。");
    }
}
