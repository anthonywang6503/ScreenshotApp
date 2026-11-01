using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

public class EyedropperTests
{
    [Theory]
    [InlineData(-0.001, 0)]
    [InlineData(0, -0.001)]
    [InlineData(200, 10)]
    [InlineData(199.9999999, 0)]
    [InlineData(10, 120)]
    [InlineData(double.NaN, 0)]
    public void AT12_OutsideImage_DoesNotClampOrLeaveTool(double x, double y) => StaRunner.Run(() =>
    {
        var session = RulerTests.Session();
        session.BeginEyedropper();
        Assert.False(session.PickColor(new Point(x, y)));
        Assert.Equal(EditorTool.Eyedropper, session.Tool);
        Assert.Equal(Colors.Red, session.Style.Color);
        Assert.Equal(0, session.Document.History.UndoCount);
        Assert.True(session.CancelOperation());
        Assert.Equal(EditorTool.Select, session.Tool);
    });

    [Fact]
    public void AT12_SelectedAnnotation_UsesOriginalPixelAndOneUndoableCommand() => StaRunner.Run(() =>
    {
        var session = RulerTests.Session();
        var rectangle = new RectangleAnnotation { Color = Colors.Lime, StrokeWidth = 50, Rect = new(10, 10, 50, 50) };
        session.Document.History.Execute(new AddAnnotationCommand(session.Document, rectangle));
        session.Select(rectangle);
        session.BeginEyedropper();
        session.PointerDown(new Point(10, 10), 2, 1);
        Assert.Equal("#112233", session.PickedColorCode);
        Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), session.Selected!.Color);
        Assert.Equal(Colors.Red, session.Style.Color);
        Assert.Equal(2, session.Document.History.UndoCount);
        Assert.Equal(EditorTool.Select, session.Tool);
        Assert.False(session.IsDragging);
        Assert.True(session.Undo());
        Assert.Equal(rectangle, session.Document.Annotations[0]);
    });

    [Fact]
    public void AT12_NoSelection_UpdatesDefaultsAndReturnsToPreviousTool() => StaRunner.Run(() =>
    {
        var session = RulerTests.Session();
        var changes = 0;
        session.StyleDefaultsChanged += (_, _) => changes++;
        session.SetTool(EditorTool.Rectangle);
        session.BeginEyedropper();
        Assert.True(session.PickColor(new Point(199.9, 119.9)));
        Assert.Equal(EditorTool.Rectangle, session.Tool);
        Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), session.Style.Color);
        Assert.Equal(1, changes);
        Assert.Equal(0, session.Document.History.UndoCount);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AT12_TextEdit_IsRetainedAndOnlyCommitCreatesCommand(bool existing) => StaRunner.Run(() =>
    {
        var session = RulerTests.Session();
        if (existing)
        {
            session.Document.History.Execute(new AddAnnotationCommand(session.Document,
                new TextAnnotation { Origin = new(20, 20), Text = "原文字", Color = Colors.Red, FontFamily = "Segoe UI", FontSize = 16 }));
        }
        session.SetTool(EditorTool.Text);
        session.PointerDown(new Point(20, 20), 2, 1);
        var count = session.Document.History.UndoCount;
        session.BeginEyedropper();
        Assert.NotNull(session.TextEdit);
        Assert.True(session.PickColor(new Point(50, 50)));
        Assert.Equal(EditorTool.Text, session.Tool);
        Assert.Equal(count, session.Document.History.UndoCount);
        Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), session.TextEdit!.Color);
        session.CommitTextEdit("台灣繁體中文");
        Assert.Equal(count + 1, session.Document.History.UndoCount);
        Assert.Equal("台灣繁體中文", Assert.IsType<TextAnnotation>(Assert.Single(session.Document.Annotations)).Text);
    });

    [Fact]
    public void AT12_EffectSelection_IgnoresColor_AndCancelRetainsTextAndCrop() => StaRunner.Run(() =>
    {
        var session = RulerTests.Session();
        var effect = new MosaicAnnotation { Color = Colors.Transparent, Rect = new(10, 10, 20, 20), Strength = 3 };
        session.Document.History.Execute(new AddAnnotationCommand(session.Document, effect));
        session.Select(effect);
        session.BeginEyedropper();
        Assert.True(session.PickColor(new Point(15, 15)));
        Assert.Equal(effect, session.Selected);
        Assert.Equal(1, session.Document.History.UndoCount);
        session.SetTool(EditorTool.Crop);
        var crop = session.PendingCrop;
        session.BeginEyedropper();
        session.CancelOperation();
        Assert.Equal(crop, session.PendingCrop);
        Assert.Equal(EditorTool.Crop, session.Tool);
        session.SetTool(EditorTool.Text);
        session.PointerDown(new Point(90, 80), 1, 1);
        var edit = session.TextEdit;
        session.BeginEyedropper();
        session.CancelOperation();
        Assert.Same(edit, session.TextEdit);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AT12_IndexedAndPremultipliedImages_AreSampledAsRgb(bool indexed) => StaRunner.Run(() =>
    {
        var image = indexed
            ? BitmapSource.Create(1, 1, 96, 96, PixelFormats.Indexed8, new BitmapPalette([Colors.Lime]), new byte[] { 0 }, 1)
            : BitmapSource.Create(1, 1, 96, 96, PixelFormats.Pbgra32, null, new byte[] { 0, 128, 0, 128 }, 4);
        image.Freeze();
        var session = new EditorSession(new ImageDocument(image), new AnnotationStyle(Colors.Red, 2, "Segoe UI", 16));
        session.BeginEyedropper();
        Assert.True(session.PickColor(new Point(0, 0)));
        Assert.Equal(Colors.Lime, session.Style.Color);
    });
}
