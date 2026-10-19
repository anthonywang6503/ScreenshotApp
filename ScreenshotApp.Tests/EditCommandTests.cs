using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Tests;

/// <summary>
/// 驗證文件模型與六種編輯命令：執行後復原會還原狀態、重複執行無害、刪除後復原回到原本的層序位置、裁切不改寫標註座標、
/// 複合命令依序執行並以相反順序復原，以及文件拒絕未凍結的圖片、重複的 Id 與無效的裁切範圍。
/// </summary>
public class EditCommandTests
{
    [Fact]
    public void Constructor_NonFrozenSource_ThrowsArgumentException()
    {
        var source = CreateBitmap(20, 10);

        var exception = Assert.Throws<ArgumentException>(() => new ImageDocument(source));

        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void Constructor_FrozenSource_StartsWithFullCropAndEmptyHistory()
    {
        var document = CreateDocument(200, 100);

        Assert.Equal(200, document.PixelWidth);
        Assert.Equal(100, document.PixelHeight);
        Assert.Equal(new PixelRect(0, 0, 200, 100), document.Bounds);
        Assert.Equal(document.Bounds, document.CropRect);
        Assert.Empty(document.Annotations);
        Assert.False(document.History.CanUndo);
    }

    [Fact]
    public void NextStepNumber_WithoutSteps_IsOne()
    {
        var document = CreateDocument();
        Assert.Equal(1, document.NextStepNumber);

        Add(document, Rectangle(new PixelRect(0, 0, 10, 10)));

        Assert.Equal(1, document.NextStepNumber); // 其他種類的標註不影響編號
    }

    [Fact]
    public void NextStepNumber_IsMaxExistingNumberPlusOne()
    {
        var document = CreateDocument();
        Add(document, Step(5));
        Add(document, Step(2));

        Assert.Equal(6, document.NextStepNumber);
    }

    [Fact]
    public void NextStepNumber_AfterDeletingAStep_KeepsOtherNumbersUnchanged()
    {
        var document = CreateDocument();
        var first = Add(document, Step(1));
        var second = Add(document, Step(2));
        var third = Add(document, Step(3));

        document.History.Execute(new RemoveAnnotationCommand(document, second));

        Assert.Equal(new Annotation[] { first, third }, document.Annotations);
        Assert.Equal(4, document.NextStepNumber);
    }

    [Fact]
    public void Insert_DuplicateId_ThrowsAndKeepsDocumentUnchanged()
    {
        var document = CreateDocument();
        var rectangle = Rectangle(new PixelRect(0, 0, 10, 10));
        document.Insert(0, rectangle);

        Assert.Throws<InvalidOperationException>(() => document.Insert(1, rectangle with { Color = Colors.Blue }));

        Assert.Same(rectangle, Assert.Single(document.Annotations));
    }

    [Theory]
    [InlineData(0, 0, 0, 10)] // 空範圍
    [InlineData(0, 0, 10, 0)]
    [InlineData(-1, 0, 10, 10)] // 超出左邊
    [InlineData(0, -1, 10, 10)] // 超出上邊
    [InlineData(191, 0, 10, 10)] // 超出右邊
    [InlineData(0, 91, 10, 10)] // 超出下邊
    [InlineData(0, 0, 201, 100)]
    [InlineData(0, 0, 200, 101)]
    public void SetCropCommand_EmptyOrOutOfBoundsRect_ThrowsAndIsNotRecorded(int x, int y, int width, int height)
    {
        var document = CreateDocument(200, 100);
        var command = new SetCropCommand(document, document.CropRect, new PixelRect(x, y, width, height));

        Assert.Throws<ArgumentOutOfRangeException>(() => document.History.Execute(command));

        Assert.Equal(document.Bounds, document.CropRect);
        Assert.False(document.History.CanUndo);
    }

    [Theory]
    [InlineData(50, 20, 100, 60)]
    [InlineData(0, 0, 200, 100)] // 整張圖片
    [InlineData(199, 99, 1, 1)] // 右下角的單一像素
    public void SetCropCommand_ExecuteThenUndo_ChangesOnlyTheCropRect(int x, int y, int width, int height)
    {
        var document = CreateDocument(200, 100);
        var source = document.Source;
        Add(document, Rectangle(new PixelRect(10, 10, 30, 20)));
        Add(document, new LineAnnotation { Color = Colors.Red, Start = new PixelPoint(5, 5), End = new PixelPoint(150, 90), StrokeWidth = 2 });
        Add(document, new TextAnnotation { Color = Colors.Red, Origin = new PixelPoint(120, 10), Text = "標註", FontFamily = "Segoe UI", FontSize = 16 });
        Add(document, Step(1));
        var annotationsBefore = document.Annotations.ToList();
        var crop = new PixelRect(x, y, width, height);
        var command = new SetCropCommand(document, document.CropRect, crop);

        command.Execute();
        command.Execute();

        Assert.Equal(crop, document.CropRect);
        Assert.Equal(annotationsBefore, document.Annotations); // 標註座標仍以原始圖片為準，不因裁切改寫
        Assert.Same(source, document.Source);
        Assert.Equal(new PixelRect(0, 0, 200, 100), document.Bounds);

        command.Undo();
        command.Undo();

        Assert.Equal(document.Bounds, document.CropRect);
        Assert.Equal(annotationsBefore, document.Annotations);
    }

    [Fact]
    public void AddAnnotationCommand_ExecuteThenUndo_RestoresStateAndRepeatsHarmlessly()
    {
        var document = CreateDocument();
        var existing = Add(document, Rectangle(new PixelRect(0, 0, 10, 10)));
        var added = Rectangle(new PixelRect(20, 20, 10, 10));
        var command = new AddAnnotationCommand(document, added);

        command.Execute();
        command.Execute();

        Assert.Equal(new Annotation[] { existing, added }, document.Annotations); // 加在最上層，且只有一份

        command.Undo();
        command.Undo();

        Assert.Same(existing, Assert.Single(document.Annotations));
    }

    [Fact]
    public void RemoveAnnotationCommand_Undo_RestoresOriginalIndex()
    {
        var document = CreateDocument();
        var bottom = Add(document, Rectangle(new PixelRect(0, 0, 10, 10)));
        var middle = Add(document, Rectangle(new PixelRect(10, 10, 10, 10)));
        var top = Add(document, Rectangle(new PixelRect(20, 20, 10, 10)));
        var command = new RemoveAnnotationCommand(document, middle);

        command.Execute();
        command.Execute();

        Assert.Equal(new Annotation[] { bottom, top }, document.Annotations);

        command.Undo();
        command.Undo();

        Assert.Equal(new Annotation[] { bottom, middle, top }, document.Annotations);
    }

    // 由下而上刪除時每一筆刪除的都是當時的索引 0：只有以相反順序復原，三個標註才會回到原本的層序。
    [Fact]
    public void CompositeCommand_UndoRunsInReverseOrderAndRepeatsHarmlessly()
    {
        var document = CreateDocument();
        var bottom = Add(document, Rectangle(new PixelRect(0, 0, 10, 10)));
        var middle = Add(document, Rectangle(new PixelRect(10, 10, 10, 10)));
        var top = Add(document, Rectangle(new PixelRect(20, 20, 10, 10)));
        var command = new CompositeCommand(
        [
            new RemoveAnnotationCommand(document, bottom),
            new RemoveAnnotationCommand(document, middle),
            new RemoveAnnotationCommand(document, top),
        ]);

        command.Execute();

        Assert.Empty(document.Annotations);

        command.Undo();

        Assert.Equal(new Annotation[] { bottom, middle, top }, document.Annotations);

        command.Execute();
        command.Execute();
        command.Undo();
        command.Undo();

        Assert.Equal(new Annotation[] { bottom, middle, top }, document.Annotations);
    }

    [Fact]
    public void ReplaceAnnotationCommand_DifferentId_ThrowsArgumentException()
    {
        var document = CreateDocument();
        var before = Add(document, Rectangle(new PixelRect(0, 0, 10, 10)));
        var other = Rectangle(new PixelRect(0, 0, 10, 10));

        Assert.Throws<ArgumentException>(() => new ReplaceAnnotationCommand(document, before, other));
    }

    [Fact]
    public void ReplaceAnnotationCommand_ExecuteThenUndo_SwapsValueInPlace()
    {
        var document = CreateDocument();
        var bottom = Add(document, Rectangle(new PixelRect(0, 0, 10, 10)));
        var before = Add(document, Rectangle(new PixelRect(10, 10, 10, 10)));
        var top = Add(document, Rectangle(new PixelRect(20, 20, 10, 10)));
        var after = before with { Rect = new PixelRect(15, 12, 30, 20), Color = Colors.Blue };
        var command = new ReplaceAnnotationCommand(document, before, after);

        command.Execute();
        command.Execute();

        Assert.Equal(new Annotation[] { bottom, after, top }, document.Annotations);

        command.Undo();
        command.Undo();

        Assert.Equal(new Annotation[] { bottom, before, top }, document.Annotations);
    }

    [Theory]
    [InlineData(0, 3)] // 最底層移到最上層
    [InlineData(0, 2)]
    [InlineData(3, 0)] // 最上層移到最底層
    [InlineData(2, 1)]
    public void ReorderAnnotationCommand_MovesToIndexAndBack(int fromIndex, int toIndex)
    {
        var document = CreateDocument();
        var annotations = new List<Annotation>();
        for (var i = 0; i < 4; i++)
        {
            annotations.Add(Add(document, Rectangle(new PixelRect(i * 20, 0, 10, 10))));
        }

        var moved = annotations[fromIndex];
        var expected = new List<Annotation>(annotations);
        expected.Remove(moved);
        expected.Insert(toIndex, moved);
        var command = new ReorderAnnotationCommand(document, moved.Id, fromIndex, toIndex);

        command.Execute();
        command.Execute();

        Assert.Equal(expected, document.Annotations);
        Assert.Equal(toIndex, document.IndexOf(moved.Id));

        command.Undo();
        command.Undo();

        Assert.Equal(annotations, document.Annotations);
    }

    [Fact]
    public void History_AllCommandKinds_UndoAllRestoresInitialStateAndRedoAllRestoresFinalState()
    {
        var document = CreateDocument(200, 100);
        var rectangle = Rectangle(new PixelRect(0, 0, 10, 10));
        var step = Step(1);
        var moved = rectangle with { Rect = new PixelRect(5, 5, 10, 10) };
        var crop = new PixelRect(5, 5, 50, 40);

        document.History.Execute(new AddAnnotationCommand(document, rectangle));
        document.History.Execute(new AddAnnotationCommand(document, step));
        document.History.Execute(new ReplaceAnnotationCommand(document, rectangle, moved));
        document.History.Execute(new ReorderAnnotationCommand(document, rectangle.Id, 0, 1));
        document.History.Execute(new SetCropCommand(document, document.CropRect, crop));
        document.History.Execute(new RemoveAnnotationCommand(document, step));

        Assert.Same(moved, Assert.Single(document.Annotations));

        for (var i = 0; i < 6; i++)
        {
            Assert.True(document.History.Undo());
        }

        Assert.False(document.History.Undo());
        Assert.Empty(document.Annotations);
        Assert.Equal(document.Bounds, document.CropRect);

        for (var i = 0; i < 6; i++)
        {
            Assert.True(document.History.Redo());
        }

        Assert.False(document.History.Redo());
        Assert.Same(moved, Assert.Single(document.Annotations));
        Assert.Equal(crop, document.CropRect);
    }

    private static BitmapSource CreateBitmap(int width, int height)
    {
        var stride = width * 4;
        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, new byte[stride * height], stride);
    }

    private static ImageDocument CreateDocument(int width = 200, int height = 100)
    {
        var source = CreateBitmap(width, height);
        source.Freeze();
        return new ImageDocument(source);
    }

    private static T Add<T>(ImageDocument document, T annotation)
        where T : Annotation
    {
        document.History.Execute(new AddAnnotationCommand(document, annotation));
        return annotation;
    }

    private static RectangleAnnotation Rectangle(PixelRect rect) =>
        new() { Color = Colors.Red, Rect = rect, StrokeWidth = 3 };

    private static StepNumberAnnotation Step(int number) =>
        new() { Color = Colors.Red, Center = new PixelPoint(20 * number, 50), Radius = 8, Number = number };
}
