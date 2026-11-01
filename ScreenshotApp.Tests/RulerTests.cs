using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Capture;
using ScreenshotApp.Editor;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Export;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

public class RulerTests
{
    [Theory]
    [InlineData(10, 20, 13, 24, 3, 4, 5, 4, 5, "5")]
    [InlineData(13, 24, 10, 20, -3, -4, 5, 4, 5, "5")]
    [InlineData(0, 0, 100, 0, 100, 0, 100, 101, 1, "100")]
    [InlineData(0, 0, 0, -100, 0, -100, 100, 1, 101, "100")]
    [InlineData(5, 5, 5, 5, 0, 0, 0, 1, 1, "0")]
    [InlineData(0, 0, 1, 1, 1, 1, 1.4142135623730951, 2, 2, "1.4")]
    [InlineData(0, 0, 2, 3, 2, 3, 3.605551275463989, 3, 4, "3.6")]
    public void AT14_Measurement_UsesPixelCentersAndInclusiveBounds(int x1, int y1, int x2, int y2,
        double dx, double dy, double length, double width, double height, string display)
    {
        var value = new PixelMeasurement(new(x1, y1), new(x2, y2));
        Assert.Equal(dx, value.DeltaX);
        Assert.Equal(dy, value.DeltaY);
        Assert.Equal(length, value.Length, 10);
        Assert.Equal(width, value.Width);
        Assert.Equal(height, value.Height);
        Assert.Equal(display, value.DisplayLength);
    }

    [Fact]
    public void AT14_LargeCoordinates_DoNotOverflowIntegerArithmetic()
    {
        var value = new PixelMeasurement(new(int.MinValue, 0), new(int.MaxValue, 0));
        Assert.Equal(4294967295d, value.Length);
        Assert.Equal(4294967296d, value.Width);
    }

    [Theory]
    [InlineData(0, 0, 1, 1, 200, 100)]
    [InlineData(199, 99, 198, 98, 200, 100)]
    [InlineData(0, 0, 0, 0, 1, 1)]
    public void AT14_LabelStaysInsideImage_AndVisiblePartsAreHittable(int x1, int y1, int x2, int y2, int width, int height) => StaRunner.Run(() =>
    {
        var ruler = CreateRuler(new(x1, y1), new(x2, y2), new(0, 0, width, height));
        Assert.True(new Rect(0, 0, width, height).Contains(ruler.LabelBounds));
        Assert.True(ruler.HitTest(ruler.LabelBounds.TopLeft, 0));
        Assert.True(ruler.HitTest(ruler.StartPoint + ruler.TickOffset, 0));
        Assert.False(ruler.HitTest(new Point(400, 400), 0));
        Assert.Equal(2, ruler.Handles.Count);
    });

    [Fact]
    public void AT14_CreateResizeMoveStyleDuplicateDelete_AreOneCommandEach() => StaRunner.Run(() =>
    {
        var session = Session();
        session.SetTool(EditorTool.Ruler);
        session.PointerDown(new Point(20, 30), 1, 1);
        session.PointerMove(new Point(80, 30));
        Assert.Empty(session.Document.Annotations);
        Assert.Equal("60", session.RulerMeasurement!.Value.DisplayLength);
        session.PointerUp(new Point(80, 30));
        var original = Assert.IsType<RulerAnnotation>(Assert.Single(session.Document.Annotations));
        Assert.Equal(1, session.Document.History.UndoCount);
        session.PointerDown(original.EndPoint, 1, 1);
        session.PointerMove(original.EndPoint + new Vector(0, 80));
        session.PointerUp(original.EndPoint + new Vector(0, 80));
        var resized = Assert.IsType<RulerAnnotation>(session.Selected);
        Assert.Equal(100, resized.Measurement.Length);
        Assert.Equal(2, session.Document.History.UndoCount);
        session.SetTool(EditorTool.Select);
        var label = resized.LabelBounds;
        var point = new Point(label.X + label.Width / 2, label.Y + label.Height / 2);
        session.PointerDown(point, 1, 1);
        session.PointerMove(point + new Vector(5, 6));
        session.PointerUp(point + new Vector(5, 6));
        Assert.Equal(resized.Translate(5, 6), session.Selected);
        Assert.Equal(3, session.Document.History.UndoCount);
        session.SetColor(Colors.Lime);
        session.SetStrokeWidth(4);
        Assert.Equal(5, session.Document.History.UndoCount);
        Assert.True(session.Duplicate());
        Assert.True(session.DeleteSelected());
        Assert.Equal(7, session.Document.History.UndoCount);
        for (var i = 0; i < 7; i++) Assert.True(session.Undo());
        Assert.Empty(session.Document.Annotations);
        for (var i = 0; i < 7; i++) Assert.True(session.Redo());
        Assert.Single(session.Document.Annotations);
    });

    [Fact]
    public void AT14_ZeroDragAndAbortedDrag_DoNotCreateCommands() => StaRunner.Run(() =>
    {
        var session = Session();
        session.SetTool(EditorTool.Ruler);
        session.PointerDown(new Point(10, 10), 1, 1);
        session.PointerUp(new Point(10, 10));
        session.PointerDown(new Point(10, 10), 1, 1);
        session.PointerMove(new Point(80, 80));
        session.AbortDrag();
        Assert.Empty(session.Document.Annotations);
        Assert.Equal(0, session.Document.History.UndoCount);
        var ruler = CreateRuler(new(20, 30), new(80, 30), session.Document.Bounds);
        var collapsed = Assert.IsType<RulerAnnotation>(ruler.Resize(HandleKind.End, -60, 0));
        Assert.Equal("0 px", collapsed.Label);
    });

    [Theory]
    [InlineData(0.25)]
    [InlineData(1)]
    [InlineData(2)]
    public void AT14_CanvasAndExportSharePixels_AndCropDoesNotRepositionLabel(double zoom) => StaRunner.Run(() =>
    {
        var session = Session();
        var document = session.Document;
        document.History.Execute(new AddAnnotationCommand(document, CreateRuler(new(10, 40), new(190, 40), document.Bounds)));
        var output = ImageRenderer.Render(document);
        var layer = new AnnotationLayer { Session = session, Width = 200, Height = 120 };
        var root = new Grid { Width = 200, Height = 120 };
        root.Children.Add(new Image { Source = document.Source, Stretch = Stretch.Fill });
        root.Children.Add(layer);
        root.Measure(new Size(200, 120));
        root.Arrange(new Rect(0, 0, 200, 120));
        var canvas = new RenderTargetBitmap(200, 120, 96, 96, PixelFormats.Pbgra32);
        canvas.Render(root);
        var expected = Pixels(output);
        Assert.Equal(expected, Pixels(canvas));
        layer.LayoutTransform = new ScaleTransform(zoom, zoom);
        layer.Measure(new Size(800, 480));
        layer.Arrange(new Rect(0, 0, 800, 480));
        canvas.Render(layer);
        Assert.Equal(expected, Pixels(ImageRenderer.Render(document)));
        var crop = new PixelRect(85, 30, 30, 25);
        document.History.Execute(new SetCropCommand(document, document.Bounds, crop));
        var sliced = new byte[crop.Width * crop.Height * 4];
        output.CopyPixels(new Int32Rect(crop.X, crop.Y, crop.Width, crop.Height), sliced, crop.Width * 4, 0);
        var cropped = ImageRenderer.Render(document);
        Assert.Equal(sliced, Pixels(cropped));
        using var temp = new TempDirectory();
        var path = temp.Combine("ruler.png");
        PngFileService.Save(cropped, path);
        Assert.Equal(File.ReadAllBytes(path), ClipboardPayload.Create(cropped).Png);
    });

    internal static RulerAnnotation CreateRuler(PixelPoint start, PixelPoint end, PixelRect bounds) =>
        new() { Start = start, End = end, Color = Colors.Red, StrokeWidth = 2, ImageBounds = bounds };

    internal static EditorSession Session()
    {
        var pixels = new byte[200 * 120 * 4];
        for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = 0x33; pixels[i + 1] = 0x22; pixels[i + 2] = 0x11; }
        var image = BitmapSource.Create(200, 120, 96, 96, PixelFormats.Bgr32, null, pixels, 800);
        image.Freeze();
        return new EditorSession(new ImageDocument(image), new AnnotationStyle(Colors.Red, 2, "Segoe UI", 16));
    }

    private static byte[] Pixels(BitmapSource image)
    {
        var bytes = new byte[image.PixelWidth * image.PixelHeight * 4];
        image.CopyPixels(bytes, image.PixelWidth * 4, 0);
        return bytes;
    }
}
