using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ScreenshotApp.Editor;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;
using ScreenshotApp.Settings;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

/// <summary>
/// 編輯視窗的右鍵選單：依右鍵位置與目前工具選擇三份選單之一、比照左鍵改變選取，並依文件狀態設定項目的可用與顯示狀態；
/// 資源中選單的具名項目在執行期都找得到，選單項目與 Ctrl+D 接到對應的編輯操作。
/// 視窗只建立、不顯示；控制器只建立、不啟動（不註冊快捷鍵、不顯示系統匣圖示），設定與日誌寫在暫存目錄。
/// </summary>
public sealed class EditorWindowTests : IDisposable
{
    // 命中容差（圖片像素）。
    private const double Tolerance = 2;

    // 控制器沒有啟動，登入時啟動的設定不會套用，這個測試專用的子機碼不會被寫入。
    private const string UnusedRunKeyPath = @"Software\ScreenshotApp.Tests\EditorWindowTests\Run";

    // 200×100 的圖片上四種標註分開放置，另有一處空白。
    private static readonly Point OnText = new(25.5, 28.5);
    private static readonly Point OnStep = new(100.5, 30.5);
    private static readonly Point OnArrow = new(155.5, 20.5);
    private static readonly Point OnRectangle = new(40.5, 61.5);
    private static readonly Point OnEmptyCanvas = new(150.5, 80.5);

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void PrepareContextMenu_EmptyCanvasOfUneditedDocument_UsesCanvasMenuWithNothingToUndoResetOrClear()
    {
        RunWithWindow(() => CreateDocument(), (window, _) =>
        {
            var menu = window.PrepareContextMenu(OnEmptyCanvas, Tolerance);

            Assert.Same(window.Resources["CanvasMenu"], menu);
            Assert.False(Item(menu, "UndoMenuItem").IsEnabled);
            Assert.False(Item(menu, "RedoMenuItem").IsEnabled);
            Assert.True(Item(menu, "CopyMenuItem").IsEnabled);
            Assert.True(Item(menu, "SaveMenuItem").IsEnabled);
            Assert.False(Item(menu, "CanvasResetCropMenuItem").IsEnabled);
            Assert.False(Item(menu, "ClearAnnotationsMenuItem").IsEnabled);
        });
    }

    [Fact]
    public void CanvasMenu_OnEditedCroppedDocument_EnablesItemsThatRunTheirOperations()
    {
        RunWithWindow(CreateAnnotatedCroppedDocument, (window, document) =>
        {
            var menu = window.PrepareContextMenu(OnEmptyCanvas, Tolerance);

            Assert.Same(window.Resources["CanvasMenu"], menu);
            Assert.True(Item(menu, "UndoMenuItem").IsEnabled);
            Assert.False(Item(menu, "RedoMenuItem").IsEnabled);
            Assert.True(Item(menu, "CanvasResetCropMenuItem").IsEnabled);
            Assert.True(Item(menu, "ClearAnnotationsMenuItem").IsEnabled);

            Click(Item(menu, "CanvasResetCropMenuItem"));

            Assert.Equal(document.Bounds, document.CropRect);

            Click(Item(menu, "ClearAnnotationsMenuItem"));

            Assert.Empty(document.Annotations);

            Click(Item(menu, "UndoMenuItem"));

            Assert.Equal(4, document.Annotations.Count);
        });
    }

    // 點到的標註成為選取；只適用於特定種類的項目只在該種類上顯示。
    [Theory]
    [InlineData(nameof(OnText), true, false, false)]
    [InlineData(nameof(OnStep), false, true, false)]
    [InlineData(nameof(OnArrow), false, false, true)]
    [InlineData(nameof(OnRectangle), false, false, false)]
    public void PrepareContextMenu_OnAnnotation_UsesAnnotationMenuWithItemsForItsKind(string target, bool text, bool step, bool arrow)
    {
        var point = target switch
        {
            nameof(OnText) => OnText,
            nameof(OnStep) => OnStep,
            nameof(OnArrow) => OnArrow,
            _ => OnRectangle,
        };
        RunWithWindow(CreateAnnotatedCroppedDocument, (window, _) =>
        {
            var menu = window.PrepareContextMenu(point, Tolerance);

            Assert.Same(window.Resources["AnnotationMenu"], menu);
            Assert.Equal(text, Shown(Item(menu, "EditTextMenuItem")));
            Assert.Equal(step, Shown(Item(menu, "EditStepNumberMenuItem")));
            Assert.Equal(arrow, Shown(Item(menu, "ReverseArrowMenuItem")));
            Assert.Equal(step, Shown(Item(menu, "RenumberStepsMenuItem")));
        });
    }

    [Fact]
    public void PrepareContextMenu_StepThenEmptyCanvas_SelectsStepThenClearsSelectionLikeLeftClick()
    {
        RunWithWindow(CreateAnnotatedCroppedDocument, (window, document) =>
        {
            var menu = window.PrepareContextMenu(OnStep, Tolerance);

            Assert.True(window.StepNumberBox.IsEnabled); // 屬性列顯示選取的步驟編號
            Assert.Equal("2", window.StepNumberBox.Text);
            Assert.True(Item(menu, "RenumberStepsMenuItem").IsEnabled); // 唯一的編號是 2，可以重新編號為 1

            Click(Item(menu, "RenumberStepsMenuItem"));

            Assert.Equal(1, document.Annotations.OfType<StepNumberAnnotation>().Single().Number);

            window.PrepareContextMenu(OnEmptyCanvas, Tolerance);

            Assert.False(window.StepNumberBox.IsEnabled);
        });
    }

    [Fact]
    public void PrepareContextMenu_InCropTool_UsesCropMenuWithoutSelectingAndResetLeavesCropTool()
    {
        RunWithWindow(CreateAnnotatedCroppedDocument, (window, document) =>
        {
            window.CropToolButton.IsChecked = true;

            var menu = window.PrepareContextMenu(OnStep, Tolerance);

            Assert.Same(window.Resources["CropMenu"], menu);
            Assert.False(window.StepNumberBox.IsEnabled); // 裁切工具中不選取標註
            Assert.True(Item(menu, "CropResetCropMenuItem").IsEnabled);

            Click(Item(menu, "CropResetCropMenuItem"));

            Assert.Equal(document.Bounds, document.CropRect);
            Assert.True(window.SelectToolButton.IsChecked);
        });
    }

    [Fact]
    public void PrepareContextMenu_InCropToolWithoutCrop_DisablesResetCrop()
    {
        RunWithWindow(() => CreateDocument(), (window, _) =>
        {
            window.CropToolButton.IsChecked = true;

            var menu = window.PrepareContextMenu(OnEmptyCanvas, Tolerance);

            Assert.Same(window.Resources["CropMenu"], menu);
            Assert.False(Item(menu, "CropResetCropMenuItem").IsEnabled);
        });
    }

    [Fact]
    public void CtrlD_WithSelectedAnnotation_DuplicatesItOnTop()
    {
        RunWithWindow(CreateAnnotatedCroppedDocument, (window, document) =>
        {
            var binding = Assert.Single(
                window.InputBindings.OfType<KeyBinding>(), binding => binding.Key == Key.D && binding.Modifiers == ModifierKeys.Control);
            window.PrepareContextMenu(OnRectangle, Tolerance);
            var rectangle = document.Annotations.OfType<RectangleAnnotation>().Single();

            ((RoutedCommand)binding.Command).Execute(null, window.ContentRoot);

            // 矩形下緣 90 再往下 10 會超出裁切範圍的下緣 95，複本改往上偏移。
            Assert.Equal(5, document.Annotations.Count);
            Assert.Equal(rectangle.Translate(10, -10) with { Id = document.Annotations[^1].Id }, document.Annotations[^1]);
        });
    }

    private void RunWithWindow(Func<ImageDocument> createDocument, Action<EditorWindow, ImageDocument> test) =>
        StaRunner.Run(() =>
        {
            using var controller = new AppController(
                new SettingsStore(_temp.Combine("settings.json")),
                new StartupService(Registry.CurrentUser, UnusedRunKeyPath, "ScreenshotApp", _temp.Combine("ScreenshotApp.exe")),
                new ErrorLog(_temp.Combine("logs")));
            var window = new EditorWindow(controller);
            var document = createDocument();
            window.Load(document);
            test(window, document);
        });

    private static MenuItem Item(ContextMenu menu, string name)
    {
        var item = EditorWindow.MenuItemNamed(menu, name);
        Assert.NotNull(item);
        return item;
    }

    // 視窗沒有顯示時 IsVisible 一律為 false，選單項目是否顯示改看 Visibility。
    private static bool Shown(MenuItem item) => item.Visibility == Visibility.Visible;

    // 與使用者點選相同：Click 處理在這裡直接執行，不經過選單關閉後的延遲。
    private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));

    private static ImageDocument CreateDocument()
    {
        var source = BitmapSource.Create(200, 100, 96, 96, PixelFormats.Bgr32, null, new byte[200 * 100 * 4], 200 * 4);
        source.Freeze();
        return new ImageDocument(source);
    }

    // 文字、步驟編號（編號 2）、箭頭與矩形框各一，並套用一次裁切；四筆新增與一筆裁切都在歷史中。
    private static ImageDocument CreateAnnotatedCroppedDocument()
    {
        var document = CreateDocument();
        var color = Colors.Red;
        Annotation[] annotations =
        [
            new TextAnnotation { Color = color, Origin = new PixelPoint(20, 20), Text = "AB 12", FontFamily = "Segoe UI", FontSize = 16 },
            new StepNumberAnnotation { Color = color, Center = new PixelPoint(100, 30), Radius = 10, Number = 2 },
            new ArrowAnnotation { Color = color, Start = new PixelPoint(130, 20), End = new PixelPoint(180, 20), StrokeWidth = 3 },
            new RectangleAnnotation { Color = color, Rect = new PixelRect(20, 60, 40, 30), StrokeWidth = 3 },
        ];
        foreach (var annotation in annotations)
        {
            document.History.Execute(new AddAnnotationCommand(document, annotation));
        }

        document.History.Execute(new SetCropCommand(document, document.Bounds, new PixelRect(5, 5, 190, 90)));
        return document;
    }
}
