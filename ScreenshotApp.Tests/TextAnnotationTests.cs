using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Tests;

/// <summary>
/// 經由編輯工作階段的文字工具驗證文字標註：非空白的文字（繁體中文、英文、表情符號與組合字元、多行）提交時恰好新增一筆，
/// 空白或取消時不建立；重新編輯既有文字時依內容替換、刪除或不產生命令，輸入期間不顯示原標註；
/// 輸入中變更樣式時，重新編輯只影響這段文字，新文字則同時成為之後的預設值。
/// </summary>
public class TextAnnotationTests
{
    // 命中容差（圖片像素）。
    private const double Tolerance = 2;

    private static readonly AnnotationStyle DefaultStyle = new(Colors.Red, 3, "Microsoft JhengHei", 16);

    [Theory]
    [InlineData("繁體中文標註")]
    [InlineData("Hello, annotation!")]
    [InlineData("表情 \U0001F600\U0001F44D\U0001F3FD\U0001F468\U0000200D\U0001F469\U0000200D\U0001F467 組合字元 e\U00000301 a\U00000308")]
    [InlineData("第一行\n第二行")]
    [InlineData("第一行\r\n第二行")]
    public void CommitNewText_NonBlankText_AddsExactlyOneTextAnnotation(string text)
    {
        var session = CreateSession();
        BeginNewText(session, new Point(30.5, 40.5));

        Assert.NotNull(session.TextEdit);
        Assert.Null(session.TextEdit.Original);
        Assert.Equal(new PixelPoint(30, 40), session.TextEdit.Origin);

        session.CommitTextEdit(text);

        Assert.Null(session.TextEdit);
        Assert.Equal(1, session.Document.History.UndoCount);
        var created = Assert.IsType<TextAnnotation>(Assert.Single(session.Document.Annotations));
        Assert.Equal(text, created.Text);
        Assert.Equal(new PixelPoint(30, 40), created.Origin);
        Assert.Equal(DefaultStyle.Color, created.Color);
        Assert.Equal(DefaultStyle.FontFamily, created.FontFamily);
        Assert.Equal(DefaultStyle.FontSize, created.FontSize);
        Assert.Same(created, session.Selected);
        Assert.True(created.Bounds.Width > 0); // 能排版出可見的範圍
        Assert.True(created.Bounds.Height > 0);
    }

    [Fact]
    public void CommitNewText_MultiLine_IsTallerAndBothLineBreakStylesMatch()
    {
        var singleLine = CommitNewText("第一行").Bounds;
        var lineFeed = CommitNewText("第一行\n第二行").Bounds;
        var carriageReturnLineFeed = CommitNewText("第一行\r\n第二行").Bounds;

        Assert.True(lineFeed.Height > singleLine.Height * 1.5);
        Assert.Equal(lineFeed.Height, carriageReturnLineFeed.Height, 6); // \r\n 只算一次換行，不會多出空白行
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    [InlineData("\n\n")]
    [InlineData("\U00003000")] // 全形空白
    [InlineData(" \r\n\t\U00003000 ")]
    public void CommitNewText_BlankOrWhitespace_CreatesNothing(string text)
    {
        var session = CreateSession();
        BeginNewText(session, new Point(30.5, 40.5));

        session.CommitTextEdit(text);

        Assert.Null(session.TextEdit);
        Assert.Empty(session.Document.Annotations);
        Assert.Equal(0, session.Document.History.UndoCount);
        Assert.Null(session.Selected);
    }

    [Fact]
    public void CancelTextEdit_NewText_CreatesNothing()
    {
        var session = CreateSession();
        BeginNewText(session, new Point(30.5, 40.5));

        session.CancelTextEdit();

        Assert.Null(session.TextEdit);
        Assert.Empty(session.Document.Annotations);
        Assert.Equal(0, session.Document.History.UndoCount);

        session.CommitTextEdit("取消之後才提交"); // 已沒有進行中的輸入

        Assert.Empty(session.Document.Annotations);
        Assert.Equal(0, session.Document.History.UndoCount);
    }

    [Theory]
    [InlineData(nameof(EditorTool.Text))]
    [InlineData(nameof(EditorTool.Select))]
    public void ReEdit_ChangedText_ReplacesOnceKeepingIdAndLayer(string tool)
    {
        var session = CreateSession();
        var (below, original, above) = AddTextBetweenRectangles(session);
        var undoCount = session.Document.History.UndoCount;

        BeginReEdit(session, tool, CenterOf(original));

        Assert.NotNull(session.TextEdit);
        Assert.Same(original, session.TextEdit.Original);
        Assert.Equal(original.Origin, session.TextEdit.Origin);

        session.CommitTextEdit("修改後的文字\n第二行");

        var updated = original with { Text = "修改後的文字\n第二行" };
        Assert.Null(session.TextEdit);
        Assert.Equal(new Annotation[] { below, updated, above }, session.Document.Annotations);
        Assert.Equal(undoCount + 1, session.Document.History.UndoCount);
        Assert.Equal(updated, session.Selected);

        Assert.True(session.Undo()); // 這筆命令是替換：復原後回到原本的文字
        Assert.Equal(new Annotation[] { below, original, above }, session.Document.Annotations);
    }

    [Theory]
    [InlineData(nameof(EditorTool.Text), "")]
    [InlineData(nameof(EditorTool.Text), "  \r\n ")]
    [InlineData(nameof(EditorTool.Select), "")]
    public void ReEdit_ClearedText_RemovesOnce(string tool, string clearedText)
    {
        var session = CreateSession();
        var (below, original, above) = AddTextBetweenRectangles(session);
        var undoCount = session.Document.History.UndoCount;

        BeginReEdit(session, tool, CenterOf(original));
        session.CommitTextEdit(clearedText);

        Assert.Equal(new Annotation[] { below, above }, session.Document.Annotations);
        Assert.Equal(undoCount + 1, session.Document.History.UndoCount);
        Assert.Null(session.Selected);

        Assert.True(session.Undo()); // 這筆命令是刪除：復原後放回原本的層序位置
        Assert.Equal(new Annotation[] { below, original, above }, session.Document.Annotations);
    }

    [Theory]
    [InlineData(nameof(EditorTool.Text))]
    [InlineData(nameof(EditorTool.Select))]
    public void ReEdit_UnchangedText_RecordsNothing(string tool)
    {
        var session = CreateSession();
        var (_, original, _) = AddTextBetweenRectangles(session);
        var undoCount = session.Document.History.UndoCount;

        BeginReEdit(session, tool, CenterOf(original));
        session.CommitTextEdit(original.Text);

        Assert.Null(session.TextEdit);
        Assert.Same(original, session.Document.Annotations[1]);
        Assert.Equal(undoCount, session.Document.History.UndoCount);
    }

    [Fact]
    public void ReEdit_WhileEditing_VisibleAnnotationsExcludeTheOriginal()
    {
        var session = CreateSession();
        var (below, original, above) = AddTextBetweenRectangles(session);
        var undoCount = session.Document.History.UndoCount;

        BeginReEdit(session, nameof(EditorTool.Text), CenterOf(original));

        Assert.Equal(new Annotation[] { below, above }, session.VisibleAnnotations); // 輸入中由輸入框顯示這段文字
        Assert.Equal(new Annotation[] { below, original, above }, session.Document.Annotations);

        session.CancelTextEdit();

        Assert.Equal(new Annotation[] { below, original, above }, session.VisibleAnnotations);
        Assert.Equal(undoCount, session.Document.History.UndoCount);
    }

    [Fact]
    public void ReEdit_StyleChanges_ApplyToThisTextOnlyAndKeepDefaults()
    {
        var session = CreateSession();
        var original = AddAnnotation(session, Text(new PixelPoint(20, 20), "原本的文字"));
        var undoCount = session.Document.History.UndoCount;
        var defaultsChanged = 0;
        session.StyleDefaultsChanged += (_, _) => defaultsChanged++;
        BeginReEdit(session, nameof(EditorTool.Text), CenterOf(original));

        session.SetColor(Colors.Blue);
        session.SetFontSize(32);
        session.SetFontFamily("Consolas");

        Assert.NotNull(session.TextEdit);
        Assert.Equal(Colors.Blue, session.TextEdit.Color);
        Assert.Equal(32, session.TextEdit.FontSize);
        Assert.Equal("Consolas", session.TextEdit.FontFamily);
        Assert.Equal(DefaultStyle, session.Style);
        Assert.Equal(0, defaultsChanged);
        Assert.Equal(undoCount, session.Document.History.UndoCount); // 輸入中只更新輸入狀態

        session.CommitTextEdit(original.Text);

        Assert.Equal(
            original with { Color = Colors.Blue, FontSize = 32, FontFamily = "Consolas" },
            Assert.Single(session.Document.Annotations));
        Assert.Equal(undoCount + 1, session.Document.History.UndoCount);
        Assert.Equal(DefaultStyle, session.Style);
        Assert.Equal(0, defaultsChanged);
    }

    [Fact]
    public void NewText_StyleChanges_AlsoBecomeDefaults()
    {
        var session = CreateSession();
        var defaultsChanged = 0;
        session.StyleDefaultsChanged += (_, _) => defaultsChanged++;
        BeginNewText(session, new Point(30.5, 40.5));

        session.SetColor(Colors.Blue);
        session.SetFontSize(32);
        session.SetFontFamily("Consolas");

        Assert.Equal(DefaultStyle with { Color = Colors.Blue, FontSize = 32, FontFamily = "Consolas" }, session.Style);
        Assert.Equal(3, defaultsChanged);
        Assert.NotNull(session.TextEdit);
        Assert.Equal(Colors.Blue, session.TextEdit.Color);
        Assert.Equal(0, session.Document.History.UndoCount);

        session.CommitTextEdit("新文字");

        var created = Assert.IsType<TextAnnotation>(Assert.Single(session.Document.Annotations));
        Assert.Equal(Colors.Blue, created.Color);
        Assert.Equal(32, created.FontSize);
        Assert.Equal("Consolas", created.FontFamily);
        Assert.Equal(1, session.Document.History.UndoCount);
    }

    private static EditorSession CreateSession()
    {
        const int width = 200;
        const int height = 100;
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, new byte[width * height * 4], width * 4);
        source.Freeze();
        return new EditorSession(new ImageDocument(source), DefaultStyle);
    }

    private static void BeginNewText(EditorSession session, Point point)
    {
        session.SetTool(EditorTool.Text);
        session.PointerDown(point, Tolerance, 1);
        session.PointerUp(point);
    }

    // 重新編輯既有文字的兩種方式：文字工具單擊，或選取工具雙擊（第一下選取並開始拖曳，原地放開不產生命令）。
    private static void BeginReEdit(EditorSession session, string tool, Point point)
    {
        session.SetTool(Enum.Parse<EditorTool>(tool));
        session.PointerDown(point, Tolerance, 1);
        session.PointerUp(point);
        if (session.Tool == EditorTool.Select)
        {
            session.PointerDown(point, Tolerance, 2);
            session.PointerUp(point);
        }
    }

    private static TextAnnotation CommitNewText(string text)
    {
        var session = CreateSession();
        BeginNewText(session, new Point(30.5, 40.5));
        session.CommitTextEdit(text);
        return Assert.IsType<TextAnnotation>(Assert.Single(session.Document.Annotations));
    }

    // 文字夾在兩個遠離它的矩形框之間，用來確認重新編輯不改變層序。
    private static (RectangleAnnotation Below, TextAnnotation Text, RectangleAnnotation Above) AddTextBetweenRectangles(EditorSession session)
    {
        var below = AddAnnotation(session, new RectangleAnnotation { Color = Colors.Green, Rect = new PixelRect(150, 60, 30, 30), StrokeWidth = 3 });
        var text = AddAnnotation(session, Text(new PixelPoint(20, 20), "原本的文字"));
        var above = AddAnnotation(session, new RectangleAnnotation { Color = Colors.Green, Rect = new PixelRect(150, 10, 30, 30), StrokeWidth = 3 });
        return (below, text, above);
    }

    private static T AddAnnotation<T>(EditorSession session, T annotation)
        where T : Annotation
    {
        session.Document.History.Execute(new AddAnnotationCommand(session.Document, annotation));
        return annotation;
    }

    private static TextAnnotation Text(PixelPoint origin, string text) =>
        new() { Color = DefaultStyle.Color, Origin = origin, Text = text, FontFamily = DefaultStyle.FontFamily, FontSize = DefaultStyle.FontSize };

    private static Point CenterOf(Annotation annotation)
    {
        var bounds = annotation.Bounds;
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }
}
