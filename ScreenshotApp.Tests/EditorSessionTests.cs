using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Capture;
using ScreenshotApp.Editor;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Tests;

/// <summary>
/// 驗證編輯工作階段把指標操作轉成命令的工具邏輯：繪製圖形、選取與移動、控制點調整、步驟編號、樣式、裁切、取消順序、
/// 復原與重做、層序調整與刪除。指標位置一律以圖片像素傳入，工作階段捨去到所在像素並限制在圖片內；拖曳期間只更新預覽，
/// 放開時才產生一筆命令；同一個圖片上的手勢在任何縮放比例下都得到相同的標註。
/// </summary>
public class EditorSessionTests
{
    // 命中容差（圖片像素）。
    private const double Tolerance = 2;

    private static readonly AnnotationStyle DefaultStyle = new(Colors.Red, 3, "Segoe UI", 16);

    [Theory]
    [InlineData(nameof(EditorTool.Rectangle), typeof(RectangleAnnotation))]
    [InlineData(nameof(EditorTool.Ellipse), typeof(EllipseAnnotation))]
    [InlineData(nameof(EditorTool.Line), typeof(LineAnnotation))]
    [InlineData(nameof(EditorTool.Arrow), typeof(ArrowAnnotation))]
    public void ShapeTool_Drag_AddsExactlyOneAnnotationAndSelectsIt(string tool, Type expectedType)
    {
        var session = CreateSession();
        session.SetTool(Enum.Parse<EditorTool>(tool));

        Drag(session, new Point(10, 10), new Point(20, 15));

        Assert.Equal(1, session.Document.History.UndoCount);
        var created = Assert.Single(session.Document.Annotations);
        Assert.IsType(expectedType, created);
        Assert.Equal(DefaultStyle.Color, created.Color);
        Assert.Same(created, session.Selected);

        Assert.True(session.Undo()); // 復原後標註消失：這筆命令是新增
        Assert.Empty(session.Document.Annotations);
    }

    [Theory]
    [InlineData(nameof(EditorTool.Rectangle), 10, 10, 20, 15)]
    [InlineData(nameof(EditorTool.Rectangle), 20, 15, 10, 10)] // 反方向拖曳得到相同範圍
    [InlineData(nameof(EditorTool.Ellipse), 10.9, 10.2, 20.5, 15.99)] // 指標位置捨去到所在的像素
    [InlineData(nameof(EditorTool.Ellipse), 20, 10, 10, 15)]
    public void BoxTool_Drag_CreatesRectIncludingStartAndEndPixels(string tool, double fromX, double fromY, double toX, double toY)
    {
        var session = CreateSession();
        session.SetTool(Enum.Parse<EditorTool>(tool));

        Drag(session, new Point(fromX, fromY), new Point(toX, toY));

        var box = (BoxAnnotation)Assert.Single(session.Document.Annotations);
        Assert.Equal(new PixelRect(10, 10, 11, 6), box.Rect);
        Assert.Equal(DefaultStyle.StrokeWidth, box.StrokeWidth);
    }

    [Theory]
    [InlineData(nameof(EditorTool.Line), 10, 10, 20, 15, 10, 10, 20, 15)]
    [InlineData(nameof(EditorTool.Arrow), 10.7, 10.2, 20.9, 15.999, 10, 10, 20, 15)] // 捨去到所在的像素
    [InlineData(nameof(EditorTool.Arrow), 20, 15, 10, 10, 20, 15, 10, 10)] // 起點與終點依拖曳方向
    public void SegmentTool_Drag_UsesPressedAndReleasedPixels(
        string tool, double fromX, double fromY, double toX, double toY, int startX, int startY, int endX, int endY)
    {
        var session = CreateSession();
        session.SetTool(Enum.Parse<EditorTool>(tool));

        Drag(session, new Point(fromX, fromY), new Point(toX, toY));

        var segment = (SegmentAnnotation)Assert.Single(session.Document.Annotations);
        Assert.Equal(new PixelPoint(startX, startY), segment.Start);
        Assert.Equal(new PixelPoint(endX, endY), segment.End);
        Assert.Equal(DefaultStyle.StrokeWidth, segment.StrokeWidth);
    }

    [Fact]
    public void ShapeTool_DuringDrag_ShowsPreviewWithoutChangingDocument()
    {
        var session = CreateSession();
        session.SetTool(EditorTool.Rectangle);

        session.PointerDown(new Point(10, 10), Tolerance, 1);
        session.PointerMove(new Point(20, 15));

        Assert.True(session.IsDragging);
        Assert.Empty(session.Document.Annotations);
        Assert.Equal(0, session.Document.History.UndoCount);
        var preview = Assert.IsType<RectangleAnnotation>(Assert.Single(session.VisibleAnnotations));
        Assert.Equal(new PixelRect(10, 10, 11, 6), preview.Rect);

        session.PointerUp(new Point(20, 15));

        Assert.False(session.IsDragging);
        Assert.Equal(preview, Assert.Single(session.Document.Annotations));
    }

    [Theory]
    [InlineData(nameof(EditorTool.Rectangle))]
    [InlineData(nameof(EditorTool.Ellipse))]
    [InlineData(nameof(EditorTool.Line))]
    [InlineData(nameof(EditorTool.Arrow))]
    public void ShapeTool_PointerEndsInStartPixel_CreatesNothing(string tool)
    {
        var session = CreateSession();
        session.SetTool(Enum.Parse<EditorTool>(tool));

        Drag(session, new Point(10.2, 10.3), new Point(10.9, 10.8)); // 只在同一個像素內移動

        session.PointerDown(new Point(40.5, 40.5), Tolerance, 1); // 離開起點像素後又回到起點像素
        session.PointerMove(new Point(60.5, 60.5));
        session.PointerMove(new Point(40.1, 40.9));
        session.PointerUp(new Point(40.1, 40.9));

        Assert.Empty(session.Document.Annotations);
        Assert.Empty(session.VisibleAnnotations);
        Assert.Equal(0, session.Document.History.UndoCount);
        Assert.Null(session.Selected);
    }

    [Fact]
    public void ShapeTool_PointerOutsideImage_IsClampedIntoImage()
    {
        var session = CreateSession(200, 100);
        session.SetTool(EditorTool.Rectangle);
        Drag(session, new Point(-20, -30), new Point(500, 400));
        session.Select(null);
        session.SetTool(EditorTool.Line);
        Drag(session, new Point(-20, 50.5), new Point(250, 50.5));

        Assert.Equal(2, session.Document.Annotations.Count);
        var rectangle = Assert.IsType<RectangleAnnotation>(session.Document.Annotations[0]);
        var line = Assert.IsType<LineAnnotation>(session.Document.Annotations[1]);
        Assert.Equal(new PixelRect(0, 0, 200, 100), rectangle.Rect);
        Assert.Equal(new PixelPoint(0, 50), line.Start);
        Assert.Equal(new PixelPoint(199, 50), line.End);
    }

    [Fact]
    public void SelectTool_Click_SelectsTopmostAnnotationWithoutCommand()
    {
        var session = CreateSession();
        AddAnnotation(session, Step(new PixelPoint(60, 40), number: 1));
        var upper = AddAnnotation(session, Step(new PixelPoint(64, 40), number: 2));
        var undoCount = session.Document.History.UndoCount;

        Click(session, new Point(62.5, 40.5));

        Assert.Same(upper, session.Selected);
        Assert.Equal(undoCount, session.Document.History.UndoCount);
    }

    [Fact]
    public void SelectTool_Drag_MovesByPixelDeltaWithOneReplaceOnRelease()
    {
        var session = CreateSession();
        var original = AddAnnotation(session, Rectangle(new PixelRect(20, 20, 40, 30)));
        var other = AddAnnotation(session, Rectangle(new PixelRect(150, 60, 20, 20)));
        var undoCount = session.Document.History.UndoCount;
        var expected = original with { Rect = new PixelRect(27, 25, 40, 30) };

        session.PointerDown(new Point(30.5, 21.5), Tolerance, 1); // 上側筆劃，遠離控制點
        session.PointerMove(new Point(35.2, 23.9));
        session.PointerMove(new Point(37.9, 26.2)); // 所在像素位移 (+7, +5)

        Assert.Equal(new Annotation[] { original, other }, session.Document.Annotations); // 拖曳中文件不變
        Assert.Equal(new Annotation[] { expected, other }, session.VisibleAnnotations); // 預覽取代原標註，層序不變
        Assert.Equal(expected, session.SelectionDisplay);
        Assert.Equal(undoCount, session.Document.History.UndoCount);

        session.PointerUp(new Point(37.9, 26.2));

        Assert.Equal(undoCount + 1, session.Document.History.UndoCount);
        Assert.Equal(new Annotation[] { expected, other }, session.Document.Annotations);
        Assert.Equal(expected, session.Selected);

        Assert.True(session.Undo()); // 這筆命令是替換：復原後回到原位置
        Assert.Equal(new Annotation[] { original, other }, session.Document.Annotations);
    }

    [Fact]
    public void SelectTool_PressAndReleaseInSamePixel_RecordsNothing()
    {
        var session = CreateSession();
        var original = AddAnnotation(session, Rectangle(new PixelRect(20, 20, 40, 30)));
        var undoCount = session.Document.History.UndoCount;

        session.PointerDown(new Point(30.2, 21.2), Tolerance, 1);
        session.PointerMove(new Point(30.9, 21.9));
        session.PointerUp(new Point(30.9, 21.9));

        Assert.Same(original, session.Selected);
        Assert.Same(original, Assert.Single(session.Document.Annotations));
        Assert.Equal(undoCount, session.Document.History.UndoCount);
    }

    // 依實機驗證的操作順序：矩形、箭頭、三個步驟編號、復原與重做、完成一段文字後，以選取工具點一下箭頭（沒有移動）。
    [Fact]
    public void SelectTool_ClickOnAnnotationWithoutMoving_EndsDragSoDeleteAndUndoWork()
    {
        var session = CreateSession(600, 400);
        session.SetTool(EditorTool.Rectangle);
        Drag(session, new Point(40.5, 40.5), new Point(239.5, 139.5));
        session.SetTool(EditorTool.Arrow);
        Drag(session, new Point(300.5, 60.5), new Point(450.5, 180.5));
        var arrow = Assert.IsType<ArrowAnnotation>(session.Document.Annotations[^1]);
        session.SetTool(EditorTool.StepNumber);
        foreach (var x in new[] { 480.5, 525.5, 570.5 })
        {
            Click(session, new Point(x, 60.5));
        }

        Assert.True(session.Undo());
        Assert.True(session.Redo());
        session.SetTool(EditorTool.Text);
        Click(session, new Point(60.5, 220.5));
        session.CommitTextEdit("Hello 測試");
        var annotations = session.Document.Annotations.ToList();
        session.SetTool(EditorTool.Select);

        Click(session, new Point(375.5, 120.5));

        Assert.False(session.IsDragging);
        Assert.Same(arrow, session.Selected);
        Assert.True(session.DeleteSelected());
        Assert.DoesNotContain(arrow, session.Document.Annotations);
        Assert.True(session.Undo());
        Assert.Equal(annotations, session.Document.Annotations);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectTool_DragInterrupted_DiscardsPreviewWithoutCommand(bool useCancelOperation)
    {
        var session = CreateSession();
        var original = AddAnnotation(session, Rectangle(new PixelRect(20, 20, 40, 30)));
        var undoCount = session.Document.History.UndoCount;
        session.PointerDown(new Point(30.5, 21.5), Tolerance, 1);
        session.PointerMove(new Point(50.5, 41.5));

        if (useCancelOperation)
        {
            Assert.True(session.CancelOperation());
        }
        else
        {
            session.AbortDrag();
        }

        Assert.False(session.IsDragging);
        Assert.Same(original, Assert.Single(session.VisibleAnnotations));
        Assert.Same(original, session.Selected);

        session.PointerMove(new Point(60.5, 51.5)); // 中斷之後的指標事件不再有作用
        session.PointerUp(new Point(60.5, 51.5));

        Assert.Same(original, Assert.Single(session.Document.Annotations));
        Assert.Equal(undoCount, session.Document.History.UndoCount);
    }

    [Theory]
    [InlineData(150.5, 80.5)] // 遠離任何標註
    [InlineData(40.5, 35.5)] // 選取的矩形框的空心內側
    public void SelectTool_ClickWhereNothingIsHit_ClearsSelection(double x, double y)
    {
        var session = CreateSession();
        var rectangle = AddAnnotation(session, Rectangle(new PixelRect(20, 20, 40, 30)));
        session.Select(rectangle);
        var undoCount = session.Document.History.UndoCount;

        Click(session, new Point(x, y));

        Assert.Null(session.Selected);
        Assert.Equal(undoCount, session.Document.History.UndoCount);
    }

    [Theory]
    [InlineData(nameof(EditorTool.Select))]
    [InlineData(nameof(EditorTool.Rectangle))]
    [InlineData(nameof(EditorTool.Ellipse))]
    [InlineData(nameof(EditorTool.Line))]
    [InlineData(nameof(EditorTool.Arrow))]
    [InlineData(nameof(EditorTool.Text))]
    [InlineData(nameof(EditorTool.StepNumber))]
    public void Handle_DragOnSelectedAnnotationInAnyTool_ResizesWithOneReplace(string tool)
    {
        var session = CreateSession();
        session.SetTool(EditorTool.Rectangle);
        Drag(session, new Point(10, 10), new Point(20, 15)); // 範圍 (10,10,11,6)，右下控制點位於 (21,16)
        var original = Assert.IsType<RectangleAnnotation>(session.Selected);
        session.SetTool(Enum.Parse<EditorTool>(tool));

        Drag(session, new Point(21, 16), new Point(31, 26));

        var resized = Assert.IsType<RectangleAnnotation>(Assert.Single(session.Document.Annotations));
        Assert.Equal(original with { Rect = new PixelRect(10, 10, 21, 16) }, resized);
        Assert.Equal(2, session.Document.History.UndoCount);
        Assert.Same(resized, session.Selected);
    }

    [Fact]
    public void Handle_DragSegmentEndpoint_MovesOnlyThatEndpoint()
    {
        var session = CreateSession();
        session.SetTool(EditorTool.Arrow);
        Drag(session, new Point(10, 10), new Point(50, 30));
        var original = Assert.IsType<ArrowAnnotation>(session.Selected);

        Drag(session, new Point(50.5, 30.5), new Point(60.5, 45.5)); // 終點控制點位於像素中心 (50.5, 30.5)

        Assert.Equal(original with { End = new PixelPoint(60, 45) }, Assert.Single(session.Document.Annotations));
        Assert.Equal(2, session.Document.History.UndoCount);
    }

    [Fact]
    public void Handle_PressAndReleaseWithoutMoving_RecordsNothing()
    {
        var session = CreateSession();
        session.SetTool(EditorTool.Rectangle);
        Drag(session, new Point(10, 10), new Point(20, 15));
        var original = Assert.Single(session.Document.Annotations);

        Click(session, new Point(21, 16));

        Assert.Same(original, Assert.Single(session.Document.Annotations)); // 沒有調整，也沒有新建圖形
        Assert.Equal(1, session.Document.History.UndoCount);
    }

    [Fact]
    public void Handle_PressBeyondTolerance_StartsTheToolOperationInstead()
    {
        var session = CreateSession();
        session.SetTool(EditorTool.Rectangle);
        Drag(session, new Point(10, 10), new Point(20, 15));
        var first = Assert.Single(session.Document.Annotations);

        Drag(session, new Point(24, 16), new Point(40, 30)); // 距右下控制點 3 像素，超出容差

        Assert.Equal(2, session.Document.Annotations.Count);
        Assert.Same(first, session.Document.Annotations[0]);
    }

    [Fact]
    public void StepTool_SuccessiveClicks_PlaceNumbersOneTwoThreeWithOneCommandEach()
    {
        var session = CreateSession();
        session.SetTool(EditorTool.StepNumber);

        Click(session, new Point(20.5, 20.5));
        Assert.Equal(1, session.Document.History.UndoCount);
        Click(session, new Point(60.5, 20.5));
        Assert.Equal(2, session.Document.History.UndoCount);
        Click(session, new Point(100.5, 20.5));
        Assert.Equal(3, session.Document.History.UndoCount);

        var steps = session.Document.Annotations.Cast<StepNumberAnnotation>().ToList();
        Assert.Equal(new[] { 1, 2, 3 }, steps.Select(step => step.Number));
        Assert.Equal(new[] { new PixelPoint(20, 20), new PixelPoint(60, 20), new PixelPoint(100, 20) }, steps.Select(step => step.Center));
        Assert.All(steps, step => Assert.Equal(StepNumberAnnotation.DefaultRadius(DefaultStyle.FontSize), step.Radius));
        Assert.All(steps, step => Assert.Equal(DefaultStyle.Color, step.Color));
        Assert.Same(steps[2], session.Selected);
    }

    [Fact]
    public void StepTool_AfterDeletingMiddleStep_NextIsFourAndThirdIsUnchanged()
    {
        var session = CreateSession();
        session.SetTool(EditorTool.StepNumber);
        Click(session, new Point(20.5, 20.5));
        Click(session, new Point(60.5, 20.5));
        Click(session, new Point(100.5, 20.5));
        var second = session.Document.Annotations[1];
        var third = session.Document.Annotations[2];

        session.Select(second);
        Assert.True(session.DeleteSelected());
        Click(session, new Point(140.5, 20.5));

        Assert.Equal(new[] { 1, 3, 4 }, session.Document.Annotations.Cast<StepNumberAnnotation>().Select(step => step.Number));
        Assert.Same(third, session.Document.Annotations[1]);
    }

    [Fact]
    public void StepTool_DragBeforeRelease_MovesNewStepAndAddsOnce()
    {
        var session = CreateSession();
        session.SetTool(EditorTool.StepNumber);

        session.PointerDown(new Point(20.5, 20.5), Tolerance, 1);

        var placed = Assert.IsType<StepNumberAnnotation>(Assert.Single(session.VisibleAnnotations));
        Assert.Equal(new PixelPoint(20, 20), placed.Center);
        Assert.Empty(session.Document.Annotations); // 放開前尚未加入文件

        session.PointerMove(new Point(30.5, 25.5));
        session.PointerMove(new Point(40.5, 30.5));
        session.PointerUp(new Point(40.5, 30.5));

        var step = Assert.IsType<StepNumberAnnotation>(Assert.Single(session.Document.Annotations));
        Assert.Equal(new PixelPoint(40, 30), step.Center);
        Assert.Equal(1, step.Number);
        Assert.Equal(1, session.Document.History.UndoCount);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(16)]
    [InlineData(48)]
    public void StepTool_Radius_IsDefaultRadiusOfStyleFontSize(int fontSize)
    {
        var session = new EditorSession(CreateDocument(), DefaultStyle with { FontSize = fontSize });
        session.SetTool(EditorTool.StepNumber);

        Click(session, new Point(50.5, 50.5));

        var step = Assert.IsType<StepNumberAnnotation>(Assert.Single(session.Document.Annotations));
        Assert.Equal(StepNumberAnnotation.DefaultRadius(fontSize), step.Radius);
    }

    [Fact]
    public void SetColor_WithSelection_ReplacesSelectedAnnotationOnce()
    {
        var session = CreateSession();
        var rectangle = AddAnnotation(session, Rectangle(new PixelRect(20, 20, 40, 30)));
        session.Select(rectangle);
        var undoCount = session.Document.History.UndoCount;
        var defaultsChanged = 0;
        session.StyleDefaultsChanged += (_, _) => defaultsChanged++;

        session.SetColor(Colors.Blue);

        Assert.Equal(undoCount + 1, session.Document.History.UndoCount);
        Assert.Equal(rectangle with { Color = Colors.Blue }, session.Selected);
        Assert.Equal(DefaultStyle, session.Style);
        Assert.Equal(0, defaultsChanged);
    }

    [Fact]
    public void SetColor_WithoutSelection_ChangesDefaultsWithoutCommand()
    {
        var session = CreateSession();
        var existing = AddAnnotation(session, Rectangle(new PixelRect(20, 20, 40, 30)));
        var undoCount = session.Document.History.UndoCount;
        var defaultsChanged = 0;
        session.StyleDefaultsChanged += (_, _) => defaultsChanged++;

        session.SetColor(Colors.Blue);

        Assert.Equal(DefaultStyle with { Color = Colors.Blue }, session.Style);
        Assert.Equal(1, defaultsChanged);
        Assert.Equal(undoCount, session.Document.History.UndoCount);
        Assert.Same(existing, Assert.Single(session.Document.Annotations));

        session.SetTool(EditorTool.Rectangle);
        Drag(session, new Point(100, 50), new Point(120, 70));

        Assert.Equal(Colors.Blue, session.Document.Annotations[1].Color); // 之後新增的標註使用新的預設值
    }

    [Theory]
    [InlineData(nameof(EditorTool.Rectangle))]
    [InlineData(nameof(EditorTool.Ellipse))]
    [InlineData(nameof(EditorTool.Line))]
    [InlineData(nameof(EditorTool.Arrow))]
    public void SetStrokeWidth_WithShapeSelected_ReplacesItOnce(string tool)
    {
        var session = CreateSession();
        session.SetTool(Enum.Parse<EditorTool>(tool));
        Drag(session, new Point(10, 10), new Point(40, 30));

        session.SetStrokeWidth(8);

        var strokeWidth = session.Selected switch
        {
            BoxAnnotation box => box.StrokeWidth,
            SegmentAnnotation segment => segment.StrokeWidth,
            _ => -1,
        };
        Assert.Equal(8, strokeWidth);
        Assert.Equal(2, session.Document.History.UndoCount);
        Assert.Equal(DefaultStyle, session.Style);
    }

    [Fact]
    public void SetStrokeWidth_WithTextSelected_IsIgnored()
    {
        var session = CreateSession();
        var text = AddAnnotation(session, Text(new PixelPoint(20, 20), "標註"));
        session.Select(text);
        var undoCount = session.Document.History.UndoCount;
        var defaultsChanged = 0;
        session.StyleDefaultsChanged += (_, _) => defaultsChanged++;

        session.SetStrokeWidth(8);

        Assert.Same(text, session.Selected);
        Assert.Equal(undoCount, session.Document.History.UndoCount);
        Assert.Equal(DefaultStyle, session.Style);
        Assert.Equal(0, defaultsChanged);
    }

    [Fact]
    public void SetStrokeWidth_WithoutSelection_ChangesDefaultsWithoutCommand()
    {
        var session = CreateSession();
        var defaultsChanged = 0;
        session.StyleDefaultsChanged += (_, _) => defaultsChanged++;

        session.SetStrokeWidth(8);

        Assert.Equal(DefaultStyle with { StrokeWidth = 8 }, session.Style);
        Assert.Equal(1, defaultsChanged);
        Assert.Equal(0, session.Document.History.UndoCount);
    }

    [Fact]
    public void SetStepNumber_WithStepSelected_ChangesOnlyThatStep()
    {
        var session = CreateSession();
        var first = AddAnnotation(session, Step(new PixelPoint(20, 20), number: 1));
        var second = AddAnnotation(session, Step(new PixelPoint(60, 20), number: 2));
        session.Select(first);
        var undoCount = session.Document.History.UndoCount;

        session.SetStepNumber(7);

        Assert.Equal(new Annotation[] { first with { Number = 7 }, second }, session.Document.Annotations);
        Assert.Equal(undoCount + 1, session.Document.History.UndoCount);
        Assert.Equal(8, session.Document.NextStepNumber);
    }

    [Fact]
    public void SetStepNumber_WithoutStepSelected_DoesNothing()
    {
        var session = CreateSession();
        var step = AddAnnotation(session, Step(new PixelPoint(20, 20), number: 1));
        var rectangle = AddAnnotation(session, Rectangle(new PixelRect(60, 20, 40, 30)));
        var undoCount = session.Document.History.UndoCount;

        session.SetStepNumber(7); // 沒有選取
        session.Select(rectangle);
        session.SetStepNumber(7); // 選取的不是步驟編號

        Assert.Equal(new Annotation[] { step, rectangle }, session.Document.Annotations);
        Assert.Equal(undoCount, session.Document.History.UndoCount);
        Assert.Equal(DefaultStyle, session.Style);
    }

    [Fact]
    public void SetToolCrop_ClearsSelectionAndStartsFromDocumentCrop()
    {
        var session = CreateSession(200, 100);
        var rectangle = AddAnnotation(session, Rectangle(new PixelRect(20, 20, 40, 30)));
        session.Select(rectangle);

        session.SetTool(EditorTool.Crop);

        Assert.Equal(EditorTool.Crop, session.Tool);
        Assert.Null(session.Selected);
        Assert.Equal(session.Document.CropRect, session.PendingCrop);
        Assert.Equal(new PixelRect(0, 0, 200, 100), session.PendingCrop);
    }

    [Fact]
    public void SetToolCrop_AfterAppliedCrop_StartsFromThatCrop()
    {
        var session = CreateSession(200, 100);
        EnterCropTool(session, new PixelRect(50, 20, 100, 60));
        Assert.True(session.ApplyCrop());

        session.SetTool(EditorTool.Crop);

        Assert.Equal(new PixelRect(50, 20, 100, 60), session.PendingCrop);
    }

    [Fact]
    public void CropTool_DragInsideFullImageCrop_CreatesSpanningRectWithoutCommand()
    {
        var session = CreateSession(200, 100);
        session.SetTool(EditorTool.Crop);

        session.PointerDown(new Point(50.5, 20.5), Tolerance, 1);
        session.PointerMove(new Point(149.5, 79.5));

        Assert.Equal(new PixelRect(50, 20, 100, 60), session.PendingCrop); // 拖曳中即為預覽

        session.PointerUp(new Point(149.5, 79.5));

        Assert.Equal(new PixelRect(50, 20, 100, 60), session.PendingCrop);
        Assert.Equal(session.Document.Bounds, session.Document.CropRect);
        Assert.Equal(0, session.Document.History.UndoCount);
    }

    // 待套用的範圍為 (50,20,100,60)：不是從控制點開始的拖曳，不論起點在範圍內外，都框選出包含起點與終點像素的新範圍。
    [Theory]
    [InlineData(70.5, 30.5, 90.5, 60.5, 70, 30, 21, 31)] // 起點在範圍內
    [InlineData(10.5, 10.5, 30.5, 90.5, 10, 10, 21, 81)] // 起點在範圍外
    [InlineData(120.5, 70.5, 10.5, 5.5, 10, 5, 111, 66)] // 由範圍內往左上拖到範圍外
    [InlineData(100.5, 50.5, 500, 500, 100, 50, 100, 50)] // 指標超出圖片：限制在圖片內
    [InlineData(10.5, 10.5, -50, -50, 0, 0, 11, 11)]
    public void CropTool_DragNotStartingOnHandle_CreatesSpanningRectWithoutCommand(
        double fromX, double fromY, double toX, double toY, int x, int y, int width, int height)
    {
        var session = CreateSession(200, 100);
        EnterCropTool(session, new PixelRect(50, 20, 100, 60));
        var expected = new PixelRect(x, y, width, height);

        session.PointerDown(new Point(fromX, fromY), Tolerance, 1);
        session.PointerMove(new Point(toX, toY));

        Assert.Equal(expected, session.PendingCrop);

        session.PointerUp(new Point(toX, toY));

        Assert.Equal(expected, session.PendingCrop);
        Assert.Equal(session.Document.Bounds, session.Document.CropRect); // 套用前文件不變
        Assert.Equal(0, session.Document.History.UndoCount);
        Assert.Equal(EditorTool.Crop, session.Tool);
    }

    [Theory]
    [InlineData(70.2, 30.2, 70.9, 30.8)] // 範圍內的同一個像素
    [InlineData(10.2, 10.2, 10.7, 10.9)] // 範圍外的同一個像素
    public void CropTool_PressAndReleaseInSamePixel_KeepsPreviousPendingCrop(double downX, double downY, double upX, double upY)
    {
        var session = CreateSession(200, 100);
        var pending = new PixelRect(50, 20, 100, 60);
        EnterCropTool(session, pending);

        session.PointerDown(new Point(downX, downY), Tolerance, 1);
        session.PointerMove(new Point(upX, upY));

        Assert.Equal(pending, session.PendingCrop);

        session.PointerUp(new Point(upX, upY));

        Assert.Equal(pending, session.PendingCrop);

        session.PointerDown(new Point(downX, downY), Tolerance, 1); // 離開起點像素後又回到起點像素
        session.PointerMove(new Point(downX + 30, downY + 30));
        session.PointerMove(new Point(upX, upY));
        session.PointerUp(new Point(upX, upY));

        Assert.Equal(pending, session.PendingCrop);
        Assert.Equal(0, session.Document.History.UndoCount);
    }

    // 待套用的範圍為 (50,20,100,60)：在控制點的位置按下並在圖片內拖曳，對應的邊跟著指標移動（位移四捨五入到整數像素）。
    [Theory]
    [InlineData(nameof(HandleKind.TopLeft), 60.4, 25.4, 60, 25, 90, 55)]
    [InlineData(nameof(HandleKind.Top), 100, 10, 50, 10, 100, 70)]
    [InlineData(nameof(HandleKind.Right), 120, 50, 50, 20, 70, 60)]
    [InlineData(nameof(HandleKind.BottomRight), 170, 90, 50, 20, 120, 70)]
    public void CropTool_DragHandle_ResizesPendingCropWithoutCommand(string handle, double toX, double toY, int x, int y, int width, int height)
    {
        var session = CreateSession(200, 100);
        var pending = new PixelRect(50, 20, 100, 60);
        EnterCropTool(session, pending);

        Drag(session, HandlePosition(pending, Enum.Parse<HandleKind>(handle)), new Point(toX, toY));

        Assert.Equal(new PixelRect(x, y, width, height), session.PendingCrop);
        Assert.Equal(session.Document.Bounds, session.Document.CropRect);
        Assert.Equal(0, session.Document.History.UndoCount);
    }

    // 在控制點的位置按下，拖到圖片邊緣或圖片外：移動的邊應停在圖片邊界上（左、上為 0；右、下為圖片的寬、高）。
    [Theory]
    [InlineData(nameof(HandleKind.TopLeft), -500, -500, 0, 0, 150, 80)]
    [InlineData(nameof(HandleKind.Top), 100, -500, 50, 0, 100, 80)]
    [InlineData(nameof(HandleKind.Left), -500, 50, 0, 20, 150, 60)]
    [InlineData(nameof(HandleKind.Left), 0, 50, 0, 20, 150, 60)] // 恰好拖到圖片左緣
    [InlineData(nameof(HandleKind.TopRight), 500, -500, 50, 0, 150, 80)]
    [InlineData(nameof(HandleKind.Right), 500, 50, 50, 20, 150, 60)]
    [InlineData(nameof(HandleKind.Right), 200, 50, 50, 20, 150, 60)] // 恰好拖到圖片右緣
    [InlineData(nameof(HandleKind.BottomRight), 500, 500, 50, 20, 150, 80)]
    [InlineData(nameof(HandleKind.Bottom), 100, 500, 50, 20, 100, 80)]
    [InlineData(nameof(HandleKind.Bottom), 100, 100, 50, 20, 100, 80)] // 恰好拖到圖片下緣
    [InlineData(nameof(HandleKind.BottomLeft), -500, 500, 0, 20, 150, 80)]
    public void CropTool_DragHandleToOrBeyondImageEdge_StopsMovedEdgeAtImageBounds(
        string handle, double toX, double toY, int x, int y, int width, int height)
    {
        var session = CreateSession(200, 100);
        var pending = new PixelRect(50, 20, 100, 60);
        EnterCropTool(session, pending);

        Drag(session, HandlePosition(pending, Enum.Parse<HandleKind>(handle)), new Point(toX, toY));

        Assert.Equal(new PixelRect(x, y, width, height), session.PendingCrop);
    }

    [Theory]
    [InlineData(nameof(HandleKind.Right), -20, 50, 1, 60)]
    [InlineData(nameof(HandleKind.Bottom), 100, -20, 100, 1)]
    [InlineData(nameof(HandleKind.TopLeft), 220, 120, 1, 1)]
    public void CropTool_DragHandleAcrossOppositeEdge_KeepsAtLeastOnePixelInsideImage(
        string handle, double toX, double toY, int expectedMinWidth, int expectedMinHeight)
    {
        var session = CreateSession(200, 100);
        var pending = new PixelRect(50, 20, 100, 60);
        EnterCropTool(session, pending);
        var press = HandlePosition(pending, Enum.Parse<HandleKind>(handle));
        var minWidth = int.MaxValue;
        var minHeight = int.MaxValue;

        // 以不到 0.2 像素的步距一路拖過對邊直到圖片外，每一步的預覽都不得為空或超出圖片。
        session.PointerDown(press, Tolerance, 1);
        for (var step = 1; step <= 1000; step++)
        {
            var t = step / 1000.0;
            session.PointerMove(new Point(press.X + (toX - press.X) * t, press.Y + (toY - press.Y) * t));
            var crop = Assert.NotNull(session.PendingCrop);
            Assert.True(session.Document.Bounds.Contains(crop), $"{crop} 超出圖片範圍");
            minWidth = Math.Min(minWidth, crop.Width);
            minHeight = Math.Min(minHeight, crop.Height);
        }

        session.PointerUp(new Point(toX, toY));

        Assert.Equal(expectedMinWidth, minWidth);
        Assert.Equal(expectedMinHeight, minHeight);
    }

    [Fact]
    public void ApplyCrop_ChangedPendingCrop_RecordsOneSetCropAndReturnsToSelect()
    {
        var session = CreateSession(200, 100);
        var rectangle = AddAnnotation(session, Rectangle(new PixelRect(20, 20, 40, 30)));
        var undoCount = session.Document.History.UndoCount;
        Assert.False(session.ApplyCrop()); // 不在裁切工具時沒有可套用的範圍
        session.SetTool(EditorTool.Crop);
        Drag(session, new Point(0, 0), new Point(30, 10)); // 拖曳整張圖片範圍的左上控制點

        Assert.True(session.ApplyCrop());

        Assert.Equal(new PixelRect(30, 10, 170, 90), session.Document.CropRect);
        Assert.Equal(undoCount + 1, session.Document.History.UndoCount);
        Assert.Equal(EditorTool.Select, session.Tool);
        Assert.Null(session.PendingCrop);
        Assert.Same(rectangle, Assert.Single(session.Document.Annotations)); // 裁切不改寫標註

        Assert.True(session.Undo());
        Assert.Equal(session.Document.Bounds, session.Document.CropRect);
    }

    [Fact]
    public void ApplyCrop_UnchangedPendingCrop_RecordsNothingAndReturnsToSelect()
    {
        var session = CreateSession();
        session.SetTool(EditorTool.Crop);

        Assert.True(session.ApplyCrop());

        Assert.Equal(0, session.Document.History.UndoCount);
        Assert.Equal(EditorTool.Select, session.Tool);
        Assert.Null(session.PendingCrop);
        Assert.Equal(session.Document.Bounds, session.Document.CropRect);
    }

    [Fact]
    public void CropTool_DoubleClickInsidePendingCrop_AppliesIt()
    {
        var session = CreateSession(200, 100);
        EnterCropTool(session, new PixelRect(50, 20, 100, 60));
        var inside = new Point(100.5, 50.5);

        session.PointerDown(inside, Tolerance, 1); // 雙擊的第一下：在同一個像素按下放開，範圍不變
        session.PointerUp(inside);

        Assert.Equal(new PixelRect(50, 20, 100, 60), session.PendingCrop);

        session.PointerDown(inside, Tolerance, 2);
        session.PointerUp(inside);

        Assert.Equal(new PixelRect(50, 20, 100, 60), session.Document.CropRect);
        Assert.Equal(1, session.Document.History.UndoCount);
        Assert.Equal(EditorTool.Select, session.Tool);
        Assert.Null(session.PendingCrop);
    }

    [Fact]
    public void CancelOperation_InCropTool_ReturnsToSelectWithoutChangingCrop()
    {
        var session = CreateSession(200, 100);
        EnterCropTool(session, new PixelRect(50, 20, 100, 60));

        Assert.True(session.CancelOperation());

        Assert.Equal(EditorTool.Select, session.Tool);
        Assert.Null(session.PendingCrop);
        Assert.Equal(session.Document.Bounds, session.Document.CropRect);
        Assert.Equal(0, session.Document.History.UndoCount);
    }

    [Fact]
    public void SetTool_LeavingCropTool_DiscardsPendingCrop()
    {
        var session = CreateSession(200, 100);
        EnterCropTool(session, new PixelRect(50, 20, 100, 60));

        session.SetTool(EditorTool.Rectangle);

        Assert.Null(session.PendingCrop);
        Assert.Equal(session.Document.Bounds, session.Document.CropRect);
        Assert.Equal(0, session.Document.History.UndoCount);

        session.SetTool(EditorTool.Crop);

        Assert.Equal(session.Document.Bounds, session.PendingCrop); // 重新進入時從文件的裁切範圍開始
    }

    [Fact]
    public void CancelOperation_InSelectTool_CancelsDragThenSelectionThenReportsNothing()
    {
        var session = CreateSession();
        var rectangle = AddAnnotation(session, Rectangle(new PixelRect(20, 20, 40, 30)));
        session.PointerDown(new Point(30.5, 21.5), Tolerance, 1);
        session.PointerMove(new Point(40.5, 31.5));

        Assert.True(session.CancelOperation()); // 先取消拖曳，保留選取
        Assert.False(session.IsDragging);
        Assert.Same(rectangle, session.Selected);

        Assert.True(session.CancelOperation()); // 再取消選取
        Assert.Null(session.Selected);

        Assert.False(session.CancelOperation()); // 沒有可取消的操作
        Assert.Same(rectangle, Assert.Single(session.Document.Annotations));
    }

    [Fact]
    public void CancelOperation_InCropTool_CancelsDragThenLeavesCropTool()
    {
        var session = CreateSession(200, 100);
        session.SetTool(EditorTool.Crop);
        session.PointerDown(new Point(50.5, 20.5), Tolerance, 1);
        session.PointerMove(new Point(149.5, 79.5));

        Assert.True(session.CancelOperation()); // 先取消拖曳：仍在裁切工具，範圍恢復
        Assert.False(session.IsDragging);
        Assert.Equal(EditorTool.Crop, session.Tool);
        Assert.Equal(session.Document.Bounds, session.PendingCrop);

        Assert.True(session.CancelOperation()); // 再離開裁切工具
        Assert.Equal(EditorTool.Select, session.Tool);

        Assert.False(session.CancelOperation()); // 沒有可取消的操作
        Assert.Equal(session.Document.Bounds, session.Document.CropRect);
    }

    [Fact]
    public void UndoRedo_ThroughSession_RestoreDocumentAndClearVanishedSelection()
    {
        var session = CreateSession();
        session.SetTool(EditorTool.Rectangle);
        Drag(session, new Point(10, 10), new Point(20, 15));
        var created = Assert.Single(session.Document.Annotations);
        session.SetColor(Colors.Blue);
        var recolored = Assert.Single(session.Document.Annotations);

        Assert.True(session.Undo()); // 復原顏色：標註仍在，選取保留
        Assert.Same(created, Assert.Single(session.Document.Annotations));
        Assert.Same(created, session.Selected);

        Assert.True(session.Undo()); // 復原新增：選取的標註已不存在，選取被清除
        Assert.Empty(session.Document.Annotations);
        Assert.Null(session.Selected);
        Assert.False(session.CanUndo);
        Assert.True(session.CanRedo);
        Assert.False(session.Undo());

        Assert.True(session.Redo());
        Assert.True(session.Redo());
        Assert.Same(recolored, Assert.Single(session.Document.Annotations));
        Assert.False(session.CanRedo);
        Assert.False(session.Redo());
    }

    [Fact]
    public void CanUndo_WhileDraggingOrEditingText_IsFalseAndUndoIsIgnored()
    {
        var session = CreateSession();
        var rectangle = AddAnnotation(session, Rectangle(new PixelRect(20, 20, 40, 30)));
        Assert.True(session.CanUndo);

        session.PointerDown(new Point(30.5, 21.5), Tolerance, 1);

        Assert.True(session.IsDragging);
        Assert.False(session.CanUndo);
        Assert.False(session.Undo());

        session.PointerUp(new Point(30.5, 21.5));

        Assert.True(session.CanUndo);

        session.SetTool(EditorTool.Text);
        session.PointerDown(new Point(150.5, 80.5), Tolerance, 1);

        Assert.NotNull(session.TextEdit);
        Assert.False(session.CanUndo);
        Assert.False(session.Undo());
        Assert.Same(rectangle, Assert.Single(session.Document.Annotations));

        session.CancelTextEdit();

        Assert.True(session.CanUndo);
    }

    // 五個矩形框，選取中間（索引 2）的一個：四種層序調整的結果各不相同。
    [Theory]
    [InlineData(nameof(ZOrderChange.BringToFront), 4)]
    [InlineData(nameof(ZOrderChange.BringForward), 3)]
    [InlineData(nameof(ZOrderChange.SendBackward), 1)]
    [InlineData(nameof(ZOrderChange.SendToBack), 0)]
    public void Reorder_FromMiddleLayer_RecordsOneCommandAndMovesToExpectedIndex(string change, int expectedIndex)
    {
        var session = CreateSession();
        var annotations = AddRectangles(session, 5);
        var selected = annotations[2];
        session.Select(selected);
        var undoCount = session.Document.History.UndoCount;

        Assert.True(session.Reorder(Enum.Parse<ZOrderChange>(change)));

        Assert.Equal(undoCount + 1, session.Document.History.UndoCount);
        Assert.Equal(expectedIndex, session.Document.IndexOf(selected.Id));
        Assert.Same(selected, session.Selected);

        Assert.True(session.Undo());
        Assert.Equal(annotations, session.Document.Annotations);
    }

    [Theory]
    [InlineData(nameof(ZOrderChange.BringToFront), 4)]
    [InlineData(nameof(ZOrderChange.BringForward), 4)]
    [InlineData(nameof(ZOrderChange.SendBackward), 0)]
    [InlineData(nameof(ZOrderChange.SendToBack), 0)]
    public void Reorder_AlreadyAtBoundary_ReturnsFalseWithoutCommand(string change, int index)
    {
        var session = CreateSession();
        var annotations = AddRectangles(session, 5);
        session.Select(annotations[index]);
        var undoCount = session.Document.History.UndoCount;

        Assert.False(session.Reorder(Enum.Parse<ZOrderChange>(change)));

        Assert.Equal(undoCount, session.Document.History.UndoCount);
        Assert.Equal(annotations, session.Document.Annotations);
    }

    [Fact]
    public void DeleteSelected_RemovesSelectedAnnotationOnceAndClearsSelection()
    {
        var session = CreateSession();
        var annotations = AddRectangles(session, 3);
        session.Select(annotations[1]);
        var undoCount = session.Document.History.UndoCount;

        Assert.True(session.DeleteSelected());

        Assert.Equal(new[] { annotations[0], annotations[2] }, session.Document.Annotations);
        Assert.Null(session.Selected);
        Assert.Equal(undoCount + 1, session.Document.History.UndoCount);
        Assert.False(session.DeleteSelected()); // 沒有選取時不做任何事

        Assert.True(session.Undo());
        Assert.Equal(annotations, session.Document.Annotations); // 復原後回到原本的層序
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(1.0)]
    [InlineData(4.0)]
    public void SameImageGesture_AtAnyZoom_ProducesIdenticalAnnotations(double zoom)
    {
        const double dpiScale = 1.75;
        var view = new ImageViewTransform(CoordinateMapper.ImageToViewScale(zoom, dpiScale), new Point(31.7, 18.3));
        var tolerance = CoordinateMapper.ViewLengthToImage(6, view);
        var session = CreateSession(200, 100);

        // 手勢以圖片座標描述，先換成畫布上的 DIP，再由畫布換回圖片座標，與實際指標事件經過的換算相同。
        Point Pointer(double x, double y) =>
            CoordinateMapper.ViewDipToImage(CoordinateMapper.ImageToViewDip(new Point(x, y), view), view);

        session.SetTool(EditorTool.Rectangle);
        session.PointerDown(Pointer(12.5, 20.5), tolerance, 1);
        session.PointerMove(Pointer(40.25, 33.75));
        session.PointerUp(Pointer(61.5, 45.5));
        session.Select(null); // 低縮放時容差涵蓋很多圖片像素，避免上一個標註的控制點攔截下一次按下

        session.SetTool(EditorTool.Arrow);
        session.PointerDown(Pointer(100, 80), tolerance, 1); // 像素格線上的點：換算誤差不可讓它落到左邊或上方的像素
        session.PointerUp(Pointer(150, 30));
        session.Select(null);

        session.SetTool(EditorTool.StepNumber);
        session.PointerDown(Pointer(30.25, 70.75), tolerance, 1);
        session.PointerMove(Pointer(40.75, 75.25));
        session.PointerUp(Pointer(40.75, 75.25));

        var expected = new Annotation[]
        {
            new RectangleAnnotation
            {
                Id = Guid.Empty, Color = DefaultStyle.Color, Rect = new PixelRect(12, 20, 50, 26), StrokeWidth = DefaultStyle.StrokeWidth,
            },
            new ArrowAnnotation
            {
                Id = Guid.Empty, Color = DefaultStyle.Color, Start = new PixelPoint(100, 80), End = new PixelPoint(150, 30), StrokeWidth = DefaultStyle.StrokeWidth,
            },
            new StepNumberAnnotation
            {
                Id = Guid.Empty, Color = DefaultStyle.Color, Center = new PixelPoint(40, 75), Radius = StepNumberAnnotation.DefaultRadius(DefaultStyle.FontSize), Number = 1,
            },
        };
        Assert.Equal(expected, session.Document.Annotations.Select(annotation => annotation with { Id = Guid.Empty }));
    }

    private static ImageDocument CreateDocument(int width = 200, int height = 100)
    {
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, new byte[width * height * 4], width * 4);
        source.Freeze();
        return new ImageDocument(source);
    }

    private static EditorSession CreateSession(int width = 200, int height = 100) => new(CreateDocument(width, height), DefaultStyle);

    private static void Drag(EditorSession session, Point from, Point to)
    {
        session.PointerDown(from, Tolerance, 1);
        session.PointerMove(to);
        session.PointerUp(to);
    }

    private static void Click(EditorSession session, Point at)
    {
        session.PointerDown(at, Tolerance, 1);
        session.PointerUp(at);
    }

    // 進入裁切工具，從整張圖片的範圍框選出 pendingCrop 作為待套用的範圍；框選起點遠離整張圖片範圍的控制點。
    private static void EnterCropTool(EditorSession session, PixelRect pendingCrop)
    {
        session.SetTool(EditorTool.Crop);
        Drag(session, new Point(pendingCrop.X + 0.5, pendingCrop.Y + 0.5), new Point(pendingCrop.Right - 0.5, pendingCrop.Bottom - 0.5));
        Assert.Equal(pendingCrop, session.PendingCrop);
    }

    private static Point HandlePosition(PixelRect rect, HandleKind kind) =>
        BoxHandles.Of(rect).First(handle => handle.Kind == kind).Position;

    private static T AddAnnotation<T>(EditorSession session, T annotation)
        where T : Annotation
    {
        session.Document.History.Execute(new AddAnnotationCommand(session.Document, annotation));
        return annotation;
    }

    private static List<Annotation> AddRectangles(EditorSession session, int count)
    {
        var annotations = new List<Annotation>();
        for (var i = 0; i < count; i++)
        {
            annotations.Add(AddAnnotation(session, Rectangle(new PixelRect(10 + i * 30, 10, 20, 20))));
        }

        return annotations;
    }

    private static RectangleAnnotation Rectangle(PixelRect rect) =>
        new() { Color = DefaultStyle.Color, Rect = rect, StrokeWidth = DefaultStyle.StrokeWidth };

    private static StepNumberAnnotation Step(PixelPoint center, int number) =>
        new() { Color = DefaultStyle.Color, Center = center, Radius = 10, Number = number };

    private static TextAnnotation Text(PixelPoint origin, string text) =>
        new() { Color = DefaultStyle.Color, Origin = origin, Text = text, FontFamily = DefaultStyle.FontFamily, FontSize = DefaultStyle.FontSize };
}
