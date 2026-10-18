using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Tests;

/// <summary>
/// 驗證文件是否有未匯出的變更：以編輯歷史的狀態版本與最後一次成功匯出時的版本比較。
/// 剛建立的文件沒有未匯出的變更；修改後復原回匯出時的狀態也沒有。
/// </summary>
public class DirtyTrackingTests
{
    [Fact]
    public void NewDocument_HasNoUnexportedChanges()
    {
        var document = CreateDocument();

        Assert.False(document.IsDirty);
        Assert.Equal(document.History.Version, document.ExportedVersion);
    }

    [Fact]
    public void AnyCommand_MakesTheDocumentDirty()
    {
        var document = CreateDocument();

        document.History.Execute(new SetCropCommand(document, document.CropRect, new PixelRect(10, 10, 50, 40)));

        Assert.True(document.IsDirty);
    }

    [Fact]
    public void UndoBackToTheExportedState_IsNotDirty()
    {
        var document = CreateDocument();
        Add(document);

        document.History.Undo();

        Assert.False(document.IsDirty);
    }

    [Fact]
    public void MarkExported_AtTheCurrentVersion_ClearsDirty()
    {
        var document = CreateDocument();
        Add(document);

        document.MarkExported(document.History.Version);

        Assert.False(document.IsDirty);
    }

    [Fact]
    public void AfterExport_EditUndoAndRedo_TrackTheExportedState()
    {
        var document = CreateDocument();
        Add(document);
        document.MarkExported(document.History.Version);

        Add(document);
        Assert.True(document.IsDirty);
        document.History.Undo();
        Assert.False(document.IsDirty);
        document.History.Undo();
        Assert.True(document.IsDirty); // 比匯出時更早的狀態也是未匯出的內容
        document.History.Redo();
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void MarkExported_WithTheVersionAtRenderTime_StaysDirtyWhenEditedDuringExport()
    {
        var document = CreateDocument();
        Add(document);
        var renderedVersion = document.History.Version;

        // 背景編碼期間使用者繼續編輯；完成時記錄的是合成當時的版本。
        Add(document);
        document.MarkExported(renderedVersion);

        Assert.True(document.IsDirty);
        document.History.Undo();
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void NewCommandAfterUndo_IsDirtyEvenIfTheDiscardedStateWasExported()
    {
        var document = CreateDocument();
        Add(document);
        document.MarkExported(document.History.Version);
        document.History.Undo();

        Add(document);

        Assert.True(document.IsDirty);
    }

    private static ImageDocument CreateDocument()
    {
        var source = BitmapSource.Create(200, 100, 96, 96, PixelFormats.Bgr32, null, new byte[200 * 4 * 100], 200 * 4);
        source.Freeze();
        return new ImageDocument(source);
    }

    private static void Add(ImageDocument document) =>
        document.History.Execute(new AddAnnotationCommand(
            document, new RectangleAnnotation { Color = Colors.Red, Rect = new PixelRect(10, 10, 30, 20), StrokeWidth = 3 }));
}
