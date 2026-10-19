using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ScreenshotApp.Capture;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Export;
using ScreenshotApp.Platform;
using ScreenshotApp.Resources;
using ScreenshotApp.Settings;

namespace ScreenshotApp.Editor;

/// <summary>
/// 圖片編輯視窗：工具列、屬性列、畫布與狀態列。應用程式只有一個編輯視窗，新文件以 <see cref="Load"/> 取代目前的文件。
/// 編輯邏輯在 <see cref="EditorSession"/>；畫布的圖片像素與 DIP 之間只經由 <see cref="CoordinateMapper"/> 換算。只在 UI 執行緒使用。
/// </summary>
internal partial class EditorWindow : Window
{
    // 命中標註與控制點的容差（DIP），在任何縮放下都是相同的螢幕距離。
    private const double HitToleranceDip = 6;

    private static readonly RoutedCommand UndoCommand = new();
    private static readonly RoutedCommand RedoCommand = new();
    private static readonly RoutedCommand CopyCommand = new();
    private static readonly RoutedCommand SaveCommand = new();
    private static readonly RoutedCommand OpenCommand = new();
    private static readonly RoutedCommand DeleteCommand = new();
    private static readonly RoutedCommand DuplicateCommand = new();
    private static readonly RoutedCommand CancelCommand = new();
    private static readonly RoutedCommand ApplyCropCommand = new();
    private static readonly RoutedCommand BringToFrontCommand = new();
    private static readonly RoutedCommand BringForwardCommand = new();
    private static readonly RoutedCommand SendBackwardCommand = new();
    private static readonly RoutedCommand SendToBackCommand = new();
    private static readonly RoutedCommand ZoomInCommand = new();
    private static readonly RoutedCommand ZoomOutCommand = new();
    private static readonly RoutedCommand ZoomFitCommand = new();

    private readonly AppController _controller;
    private readonly AnnotationLayer _annotationLayer = new() { IsHitTestVisible = false };
    private readonly HandleLayer _handleLayer = new() { IsHitTestVisible = false };
    private readonly Dictionary<RadioButton, EditorTool> _toolButtons;
    private readonly ObservableCollection<NamedColor> _colors = new(CommonColors.All);
    private readonly DispatcherTimer _messageTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    private EditorSession? _session;
    private string _fontFamily = StyleChoices.DefaultFontFamily;
    private PixelRect? _shownRegion;

    // 上次在裁切工具以外顯示的裁切範圍。回到一般狀態時若不同，代表裁切範圍改變了（套用、復原、重做或還原完整圖片）。
    private PixelRect? _shownCrop;
    private double _zoom = 1;
    private bool _fitMode = true;
    private bool _syncing;
    private bool _textEditorShown;
    private bool _spacePressed;
    private Point? _panStart;
    private Point _panStartOffset;
    private string? _message;

    // 複製或儲存進行中（背景編碼）；期間停用兩個命令，不重疊也不排隊。
    private bool _exporting;

    // 未匯出的變更已處理（或由呼叫端處理），這次關閉不再詢問。
    private bool _closeConfirmed;

    public EditorWindow(AppController controller)
    {
        _controller = controller;
        InitializeComponent();

        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(1280, workArea.Width * 0.85);
        Height = Math.Min(860, workArea.Height * 0.85);

        // 標註層位於底圖與文字輸入框之間；控制點層在縮放容器之外，以固定的螢幕尺寸繪製。
        ImageLayer.Children.Insert(1, _annotationLayer);
        ContentRoot.Children.Add(_handleLayer);
        _handleLayer.TransformProvider = CurrentTransform;

        _toolButtons = new Dictionary<RadioButton, EditorTool>
        {
            [SelectToolButton] = EditorTool.Select,
            [CropToolButton] = EditorTool.Crop,
            [RectangleToolButton] = EditorTool.Rectangle,
            [EllipseToolButton] = EditorTool.Ellipse,
            [LineToolButton] = EditorTool.Line,
            [ArrowToolButton] = EditorTool.Arrow,
            [TextToolButton] = EditorTool.Text,
            [StepToolButton] = EditorTool.StepNumber,
        };
        ColorList.ItemsSource = _colors;
        FontFamilyBox.ItemsSource = StyleChoices.FontFamilies;
        ZoomBox.ItemsSource = EditorZoom.Steps.Select(step => new ZoomChoice(step, FormatZoom(step)))
            .Prepend(new ZoomChoice(null, Strings.Get("Editor_ZoomFit")))
            .ToList();
        _messageTimer.Tick += (_, _) =>
        {
            _messageTimer.Stop();
            _message = null;
            UpdateMessage();
        };
        RegisterCommands();
    }

    private double DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    private double CurrentScale => CoordinateMapper.ImageToViewScale(_zoom, DpiScale);

    /// <summary>以新文件取代目前的文件：工具回到選取、縮放回到符合視窗，進行中的文字輸入直接捨棄。</summary>
    public void Load(ImageDocument document)
    {
        if (_session is not null)
        {
            _session.Changed -= Session_Changed;
            _session.StyleDefaultsChanged -= Session_StyleDefaultsChanged;
            _session.StepNumberEditRequested -= Session_StepNumberEditRequested;
        }

        var settings = _controller.Settings;
        _session = new EditorSession(
            document, new AnnotationStyle(settings.DefaultColor, settings.DefaultStrokeWidth, _fontFamily, settings.DefaultFontSize));
        _session.Changed += Session_Changed;
        _session.StyleDefaultsChanged += Session_StyleDefaultsChanged;
        _session.StepNumberEditRequested += Session_StepNumberEditRequested;
        _annotationLayer.Session = _session;
        _handleLayer.Session = _session;

        SourceImage.Source = document.Source;
        SourceImage.Width = ImageLayer.Width = _annotationLayer.Width = document.PixelWidth;
        SourceImage.Height = ImageLayer.Height = _annotationLayer.Height = document.PixelHeight;
        ImageSizeText.Text = Strings.Format("Editor_ImageSize", document.PixelWidth, document.PixelHeight);
        _messageTimer.Stop();
        _message = null;
        _shownRegion = null;
        _shownCrop = null;
        _fitMode = true;
        Session_Changed(this, EventArgs.Empty);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);

        // 縮放比例以實體像素定義，換到不同 DPI 的顯示器時重新換算成 DIP。
        if (_fitMode)
        {
            ApplyFit();
        }
        else
        {
            SetZoom(_zoom, fit: false);
        }
    }

    /// <summary>複製或儲存的背景編碼是否正在進行。</summary>
    public bool IsExporting => _exporting;

    /// <summary>完成進行中的文字輸入後，文件是否有未匯出的變更。</summary>
    public bool HasUnexportedChanges()
    {
        if (_session is null)
        {
            return false;
        }

        CommitTextEdit();
        return _session.Document.IsDirty;
    }

    /// <summary>
    /// 文件有未匯出的變更時詢問使用者；回傳 true 表示可以繼續（已複製、已儲存、選擇捨棄或沒有未匯出的變更）。
    /// </summary>
    public async Task<bool> ConfirmDiscardAsync()
    {
        if (!HasUnexportedChanges())
        {
            return true;
        }

        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        return UnsavedChangesDialog.Ask(this) switch
        {
            UnsavedChangesChoice.Copy => await CopyImageAsync(),
            UnsavedChangesChoice.Save => await SaveImageAsync(),
            UnsavedChangesChoice.Discard => true,
            _ => false,
        };
    }

    /// <summary>關閉而不詢問；呼叫端已處理未匯出的變更（例如結束程式前已詢問）。</summary>
    public void CloseWithoutConfirmation()
    {
        _closeConfirmed = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || e.Cancel || !HasUnexportedChanges())
        {
            return;
        }

        // 有未匯出的變更時先取消這次關閉，由控制器詢問後再關閉；關閉中的視窗不能再呼叫 Close，因此延後執行。
        e.Cancel = true;
        Dispatcher.BeginInvoke(new Action(_controller.RequestCloseEditor));
    }

    protected override void OnClosed(EventArgs e)
    {
        _messageTimer.Stop();
        base.OnClosed(e);
    }

    private void RegisterCommands()
    {
        // 文字輸入框有焦點時，按鍵先交給輸入框與輸入法；由輸入框路由來的快捷鍵一律不執行編輯器動作。
        RegisterShortcut(UndoCommand, () => _session?.Undo(), () => _session?.CanUndo == true, new KeyGesture(Key.Z, ModifierKeys.Control));
        RegisterShortcut(
            RedoCommand,
            () => _session?.Redo(),
            () => _session?.CanRedo == true,
            new KeyGesture(Key.Y, ModifierKeys.Control),
            new KeyGesture(Key.Z, ModifierKeys.Control | ModifierKeys.Shift));
        RegisterShortcut(
            CopyCommand, async () => await CopyImageAsync(), () => _session is not null && !_exporting, new KeyGesture(Key.C, ModifierKeys.Control));
        RegisterShortcut(
            DeleteCommand, () => _session?.DeleteSelected(), () => _session is { Selected: not null, IsDragging: false }, new KeyGesture(Key.Delete));
        RegisterShortcut(
            DuplicateCommand,
            () => _session?.Duplicate(),
            () => _session is { Selected: not null, IsDragging: false },
            new KeyGesture(Key.D, ModifierKeys.Control));
        RegisterShortcut(CancelCommand, CancelOperation, () => _session is not null, new KeyGesture(Key.Escape));
        RegisterShortcut(ApplyCropCommand, () => _session?.ApplyCrop(), () => _session?.Tool == EditorTool.Crop, new KeyGesture(Key.Return));
        RegisterShortcut(
            BringToFrontCommand,
            () => _session?.Reorder(ZOrderChange.BringToFront),
            CanReorder,
            new KeyGesture(Key.OemCloseBrackets, ModifierKeys.Control | ModifierKeys.Shift));
        RegisterShortcut(
            BringForwardCommand, () => _session?.Reorder(ZOrderChange.BringForward), CanReorder, new KeyGesture(Key.OemCloseBrackets, ModifierKeys.Control));
        RegisterShortcut(
            SendBackwardCommand, () => _session?.Reorder(ZOrderChange.SendBackward), CanReorder, new KeyGesture(Key.OemOpenBrackets, ModifierKeys.Control));
        RegisterShortcut(
            SendToBackCommand,
            () => _session?.Reorder(ZOrderChange.SendToBack),
            CanReorder,
            new KeyGesture(Key.OemOpenBrackets, ModifierKeys.Control | ModifierKeys.Shift));
        RegisterShortcut(
            ZoomInCommand,
            () => SetZoom(EditorZoom.ZoomIn(_zoom), fit: false),
            () => _session is not null,
            new KeyGesture(Key.OemPlus, ModifierKeys.Control),
            new KeyGesture(Key.Add, ModifierKeys.Control));
        RegisterShortcut(
            ZoomOutCommand,
            () => SetZoom(EditorZoom.ZoomOut(_zoom), fit: false),
            () => _session is not null,
            new KeyGesture(Key.OemMinus, ModifierKeys.Control),
            new KeyGesture(Key.Subtract, ModifierKeys.Control));
        RegisterShortcut(
            ZoomFitCommand, ApplyFit, () => _session is not null, new KeyGesture(Key.D0, ModifierKeys.Control), new KeyGesture(Key.NumPad0, ModifierKeys.Control));

        // 儲存與開啟在文字輸入中也可以使用：先完成輸入再儲存或詢問。
        CommandBindings.Add(new CommandBinding(
            SaveCommand, async (_, _) => await SaveImageAsync(), (_, e) => e.CanExecute = _session is not null && !_exporting));
        InputBindings.Add(new KeyBinding(SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control)));
        CommandBindings.Add(new CommandBinding(OpenCommand, (_, _) => _controller.OpenImage()));
        InputBindings.Add(new KeyBinding(OpenCommand, new KeyGesture(Key.O, ModifierKeys.Control)));

        OpenButton.Command = OpenCommand;
        UndoButton.Command = UndoCommand;
        RedoButton.Command = RedoCommand;
        CopyButton.Command = CopyCommand;
        SaveButton.Command = SaveCommand;
    }

    private void RegisterShortcut(RoutedCommand command, Action execute, Func<bool> canExecute, params KeyGesture[] gestures)
    {
        CommandBindings.Add(new CommandBinding(
            command,
            (_, _) => execute(),
            (_, e) => e.CanExecute = e.OriginalSource is not TextBox && canExecute()));
        foreach (var gesture in gestures)
        {
            InputBindings.Add(new KeyBinding(command, gesture));
        }
    }

    private bool CanReorder() => _session is { Selected: not null, IsDragging: false };

    private void Session_Changed(object? sender, EventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        UpdateDisplayedRegion();
        _annotationLayer.InvalidateVisual();
        _handleLayer.InvalidateVisual();
        SyncTextEditor();
        SyncToolButtons();
        SyncStyleControls();
        var crop = _session.PendingCrop ?? _session.Document.CropRect;
        CropSizeText.Text = Strings.Format("Editor_CropSize", crop.Width, crop.Height);
        UpdateMessage();
        CommandManager.InvalidateRequerySuggested();
    }

    private void Session_StyleDefaultsChanged(object? sender, EventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        // 字型不寫入設定檔，只在這個編輯視窗開著時沿用；顏色、線寬與字級寫入設定檔，下次啟動仍是預設值。
        var style = _session.Style;
        _fontFamily = style.FontFamily;
        var saved = _controller.SaveAnnotationDefaults(style.Color, style.StrokeWidth, style.FontSize);
        if (!saved.Succeeded)
        {
            ShowMessage(Strings.Format("Editor_DefaultsSaveFailed", saved.ErrorMessage));
        }
    }

    private void Session_StepNumberEditRequested(object? sender, EventArgs e)
    {
        // 要求在選取改變的通知之前送出，編號欄位此時可能仍是停用狀態；等屬性列更新後再移入焦點。
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (StepNumberBox.IsEnabled)
            {
                StepNumberBox.Focus();
                StepNumberBox.SelectAll();
            }
        }));
    }

    // 一般狀態只顯示裁切範圍（所見即所得）；裁切工具中顯示整張原圖，方便放大裁切範圍。
    private PixelRect DisplayedRegion() =>
        _session!.Tool == EditorTool.Crop ? _session.Document.Bounds : _session.Document.CropRect;

    private void UpdateDisplayedRegion()
    {
        var region = DisplayedRegion();

        // 顯示的裁切範圍改變時（套用、復原、重做或還原完整圖片）回到符合視窗，讓新的範圍填滿畫布；
        // 只是進出裁切工具而範圍沒有改變時，維持使用者原本的縮放。在裁切工具中復原裁切再離開時，
        // 顯示的仍是整張圖片，但裁切範圍已不同，同樣要回到符合視窗。
        var cropChanged = _session!.Tool != EditorTool.Crop && region != _shownCrop;
        if (cropChanged)
        {
            _shownCrop = region;
            _fitMode = true;
        }
        else if (region == _shownRegion)
        {
            return;
        }

        _shownRegion = region;
        ZoomHost.Width = region.Width;
        ZoomHost.Height = region.Height;
        CropOffset.X = -region.X;
        CropOffset.Y = -region.Y;
        if (_fitMode)
        {
            ApplyFit();
        }
        else
        {
            SetZoom(_zoom, fit: false);
        }
    }

    private ImageViewTransform? CurrentTransform()
    {
        if (_session is null || !ImageLayer.IsVisible)
        {
            return null;
        }

        return new ImageViewTransform(CurrentScale, ImageLayer.TranslatePoint(new Point(0, 0), ContentRoot));
    }

    private void ApplyFit()
    {
        if (_session is null)
        {
            return;
        }

        if (Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
        {
            // 視窗尚未完成版面；可視區域取得尺寸時會再計算。
            _fitMode = true;
            return;
        }

        // 只顯示裁切範圍時放大到填滿畫布；顯示整張圖片時（沒有裁切或在裁切工具中）不放大，小圖維持原尺寸。
        var region = DisplayedRegion();
        var margin = ZoomHost.Margin;
        var dpiScale = DpiScale;
        var zoom = EditorZoom.Fit(
            (Viewport.ActualWidth - margin.Left - margin.Right) * dpiScale,
            (Viewport.ActualHeight - margin.Top - margin.Bottom) * dpiScale,
            region.Width,
            region.Height,
            enlarge: region != _session.Document.Bounds);
        SetZoom(zoom, fit: true);
    }

    /// <summary>
    /// 套用縮放。<paramref name="viewportAnchor"/> 為縮放前後要維持不動的可視區域位置（預設為中央），
    /// 該處的圖片位置在縮放後仍位於同一處。
    /// </summary>
    private void SetZoom(double zoom, bool fit, Point? viewportAnchor = null)
    {
        if (_session is null)
        {
            return;
        }

        var anchor = viewportAnchor ?? new Point(Viewport.ViewportWidth / 2, Viewport.ViewportHeight / 2);
        Point? imageAnchor = CurrentTransform() is { } before
            ? CoordinateMapper.ViewDipToImage(Viewport.TranslatePoint(anchor, ContentRoot), before)
            : null;

        _zoom = zoom;
        _fitMode = fit;
        var scale = CurrentScale;
        ZoomTransform.ScaleX = scale;
        ZoomTransform.ScaleY = scale;

        // 100% 以上以最近鄰取樣，可清楚看到每個像素；縮小時使用高品質取樣。
        RenderOptions.SetBitmapScalingMode(SourceImage, zoom >= 1 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);

        if (!fit && imageAnchor is { } point)
        {
            Viewport.UpdateLayout();
            var origin = ImageLayer.TranslatePoint(new Point(0, 0), ContentRoot);
            var offset = CoordinateMapper.ScrollOffsetToKeep(point, scale, origin, anchor);
            Viewport.ScrollToHorizontalOffset(offset.X);
            Viewport.ScrollToVerticalOffset(offset.Y);
        }

        _syncing = true;
        try
        {
            var choices = (IReadOnlyList<ZoomChoice>)ZoomBox.ItemsSource;
            ZoomBox.SelectedItem = fit
                ? choices[0]
                : choices.FirstOrDefault(choice => choice.Zoom is { } step && Math.Abs(step - zoom) < 1e-9);
        }
        finally
        {
            _syncing = false;
        }

        ZoomPercentText.Text = FormatZoom(zoom);
        _handleLayer.InvalidateVisual();
    }

    private static string FormatZoom(double zoom) =>
        Strings.Format("Editor_ZoomPercent", Math.Round(zoom * 100, MidpointRounding.AwayFromZero));

    private void SyncToolButtons()
    {
        _syncing = true;
        try
        {
            foreach (var (button, tool) in _toolButtons)
            {
                button.IsChecked = tool == _session?.Tool;
            }
        }
        finally
        {
            _syncing = false;
        }

        UpdateCursor();
    }

    // 屬性列顯示目前操作對象的樣式：輸入中的文字、選取的標註，或沒有選取時新增標註的預設值；不適用的欄位停用。
    private void SyncStyleControls()
    {
        if (_session is null)
        {
            return;
        }

        var style = _session.Style;
        var color = style.Color;
        var strokeWidth = style.StrokeWidth;
        var fontFamily = style.FontFamily;
        var fontSize = style.FontSize;
        int? stepNumber = null;
        var strokeApplies = true;
        var fontApplies = true;
        if (_session.TextEdit is { } edit)
        {
            (color, fontFamily, fontSize, strokeApplies) = (edit.Color, edit.FontFamily, edit.FontSize, false);
        }
        else
        {
            switch (_session.Selected)
            {
                case BoxAnnotation box:
                    (color, strokeWidth, fontApplies) = (box.Color, box.StrokeWidth, false);
                    break;
                case SegmentAnnotation segment:
                    (color, strokeWidth, fontApplies) = (segment.Color, segment.StrokeWidth, false);
                    break;
                case TextAnnotation text:
                    (color, fontFamily, fontSize, strokeApplies) = (text.Color, text.FontFamily, text.FontSize, false);
                    break;
                case StepNumberAnnotation step:
                    (color, stepNumber, strokeApplies, fontApplies) = (step.Color, step.Number, false, false);
                    break;
            }
        }

        _syncing = true;
        try
        {
            ShowColor(color);
            SelectChoice(StrokeWidthBox, StyleChoices.StrokeWidths, strokeWidth);
            StrokeWidthBox.IsEnabled = strokeApplies;
            FontFamilyBox.SelectedItem = StyleChoices.FontFamilies.FirstOrDefault(font => font.FamilyName == fontFamily);
            FontFamilyBox.IsEnabled = fontApplies;
            SelectChoice(FontSizeBox, StyleChoices.FontSizes, fontSize);
            FontSizeBox.IsEnabled = fontApplies;
            if (!StepNumberBox.IsKeyboardFocusWithin)
            {
                StepNumberBox.Text = stepNumber?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            }

            StepNumberBox.IsEnabled = stepNumber is not null;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ShowColor(Color color)
    {
        var match = _colors.FirstOrDefault(named => named.Color == color);
        if (match is null)
        {
            // 清單只保留一個自訂色，新的自訂色取代舊的。
            if (_colors.Count > CommonColors.All.Count)
            {
                _colors.RemoveAt(_colors.Count - 1);
            }

            match = CommonColors.Custom(color);
            _colors.Add(match);
        }

        ColorList.SelectedItem = match;
    }

    private static void SelectChoice(ComboBox box, IReadOnlyList<int> choices, int value)
    {
        var items = StyleChoices.IncludeValue(choices, value);
        if (box.ItemsSource is not int[] current || !current.SequenceEqual(items))
        {
            box.ItemsSource = items;
        }

        box.SelectedItem = value;
    }

    private void SyncTextEditor()
    {
        if (_session?.TextEdit is not { } edit)
        {
            HideTextEditor();
            return;
        }

        var brush = new SolidColorBrush(edit.Color);
        brush.Freeze();
        TextEditor.FontFamily = new FontFamily(edit.FontFamily);
        TextEditor.FontSize = edit.FontSize;
        TextEditor.Foreground = brush;
        TextEditor.CaretBrush = brush;
        TextEditor.MinWidth = edit.FontSize / 2.0;
        if (!_textEditorShown)
        {
            _textEditorShown = true;

            // 先關閉再開啟復原功能以清除輸入框的復原紀錄，在輸入框內按 Ctrl+Z 不會退回到載入前的空白。
            TextEditor.IsUndoEnabled = false;
            TextEditor.Text = edit.Original?.Text ?? string.Empty;
            TextEditor.IsUndoEnabled = true;
            TextEditor.Visibility = Visibility.Visible;
            TextEditor.CaretIndex = TextEditor.Text.Length;
            TextEditor.Focus();
        }

        PositionTextEditor(edit.Origin);
    }

    private void HideTextEditor()
    {
        if (!_textEditorShown)
        {
            return;
        }

        _textEditorShown = false;
        var hadFocus = TextEditor.IsKeyboardFocusWithin;
        TextEditor.Visibility = Visibility.Collapsed;
        _handleLayer.TextEditBounds = null;
        _handleLayer.InvalidateVisual();
        if (hadFocus)
        {
            ContentRoot.Focus();
        }
    }

    /// <summary>
    /// 讓輸入框中第一個字元的位置與文字標註的起點重合：版面完成後量測輸入框內部的文字偏移（框線、留白），
    /// 完成輸入後由繪製碼畫出的文字才不會位移。
    /// </summary>
    private void PositionTextEditor(PixelPoint origin)
    {
        TextEditor.UpdateLayout();
        var first = TextEditor.GetRectFromCharacterIndex(0);
        var offset = first.IsEmpty ? new Vector() : new Vector(first.X, first.Y);
        Canvas.SetLeft(TextEditor, origin.X - offset.X);
        Canvas.SetTop(TextEditor, origin.Y - offset.Y);
        UpdateTextEditBounds();
    }

    private void UpdateTextEditBounds()
    {
        if (!_textEditorShown)
        {
            return;
        }

        _handleLayer.TextEditBounds = new Rect(
            Canvas.GetLeft(TextEditor), Canvas.GetTop(TextEditor), TextEditor.ActualWidth, TextEditor.ActualHeight);
        _handleLayer.InvalidateVisual();
    }

    private void CommitTextEdit()
    {
        if (_session?.TextEdit is not null)
        {
            _session.CommitTextEdit(TextEditor.Text);
        }
    }

    private void TextEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 輸入法組字中的按鍵由輸入法處理（Key 為 ImeProcessed），不會走到這裡。
        if (e.Key == Key.Escape)
        {
            _session?.CancelTextEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            CommitTextEdit();
            e.Handled = true;
        }
    }

    private void TextEditor_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTextEditBounds();

    private void ReturnFocusToTextEditor()
    {
        if (_session?.TextEdit is null)
        {
            return;
        }

        // 下拉選單關閉時會把焦點交回選單本身，延後到輸入處理之後再交回文字輸入框。
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_session?.TextEdit is not null)
            {
                TextEditor.Focus();
            }
        }));
    }

    private void ToolButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncing || _session is null)
        {
            return;
        }

        CommitTextEdit();
        _session.SetTool(_toolButtons[(RadioButton)sender]);
        SyncToolButtons();
    }

    private void ColorList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _session is null || ColorList.SelectedItem is not NamedColor color)
        {
            return;
        }

        _session.SetColor(color.Color);
        ReturnFocusToTextEditor();
    }

    private void CustomColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        var current = (ColorList.SelectedItem as NamedColor)?.Color ?? _session.Style.Color;
        if (ColorPicker.TryPick(this, current, out var picked))
        {
            _session.SetColor(picked);
        }

        ReturnFocusToTextEditor();
    }

    private void StrokeWidthBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && StrokeWidthBox.SelectedItem is int strokeWidth)
        {
            _session?.SetStrokeWidth(strokeWidth);
        }
    }

    private void FontFamilyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && FontFamilyBox.SelectedItem is NamedFont font)
        {
            _session?.SetFontFamily(font.FamilyName);
            ReturnFocusToTextEditor();
        }
    }

    private void FontSizeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && FontSizeBox.SelectedItem is int fontSize)
        {
            _session?.SetFontSize(fontSize);
            ReturnFocusToTextEditor();
        }
    }

    private void StepNumberBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(char.IsAsciiDigit);

    private void StepNumberBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            ApplyStepNumber();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // 先還原為目前的編號，離開欄位時就不會套用剛才輸入的數字。
            if (_session?.Selected is StepNumberAnnotation step)
            {
                StepNumberBox.Text = step.Number.ToString(CultureInfo.CurrentCulture);
            }

            ContentRoot.Focus();
            e.Handled = true;
        }
    }

    private void StepNumberBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ApplyStepNumber();

    // 只接受 1～999；無效的輸入還原為目前的編號。
    private void ApplyStepNumber()
    {
        if (_syncing || _session?.Selected is not StepNumberAnnotation step)
        {
            return;
        }

        if (int.TryParse(StepNumberBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is >= 1 and <= 999)
        {
            _session.SetStepNumber(number);
        }

        StepNumberBox.Text = (_session.Selected as StepNumberAnnotation ?? step).Number.ToString(CultureInfo.CurrentCulture);
    }

    private void ZoomBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || ZoomBox.SelectedItem is not ZoomChoice choice)
        {
            return;
        }

        if (choice.Zoom is { } zoom)
        {
            SetZoom(zoom, fit: false);
        }
        else
        {
            ApplyFit();
        }
    }

    private void Viewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_session is null || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        e.Handled = true;
        var zoom = e.Delta > 0 ? EditorZoom.ZoomIn(_zoom) : EditorZoom.ZoomOut(_zoom);
        SetZoom(zoom, fit: false, e.GetPosition(Viewport));
    }

    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 視窗大小改變不影響文件；在符合視窗模式下重新計算縮放。
        if (_fitMode)
        {
            ApplyFit();
        }
    }

    private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e) => _handleLayer.InvalidateVisual();

    private void ContentRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        e.Handled = true;
        ContentRoot.Focus();

        // 在文字輸入框外按下滑鼠即完成輸入；這次按下只用來結束輸入，不再開始其他操作。
        if (_session.TextEdit is not null)
        {
            CommitTextEdit();
            return;
        }

        if (_spacePressed)
        {
            BeginPan(e);
            return;
        }

        if (CurrentTransform() is not { } view)
        {
            return;
        }

        var point = CoordinateMapper.ViewDipToImage(e.GetPosition(ContentRoot), view);
        _session.PointerDown(point, CoordinateMapper.ViewLengthToImage(HitToleranceDip, view), e.ClickCount);
        if (_session.IsDragging)
        {
            ContentRoot.CaptureMouse();
        }
    }

    private void ContentRoot_MouseMove(object sender, MouseEventArgs e)
    {
        if (_panStart is { } start)
        {
            var position = e.GetPosition(Viewport);
            Viewport.ScrollToHorizontalOffset(_panStartOffset.X - (position.X - start.X));
            Viewport.ScrollToVerticalOffset(_panStartOffset.Y - (position.Y - start.Y));
            return;
        }

        if (_session is null || CurrentTransform() is not { } view)
        {
            return;
        }

        var point = CoordinateMapper.ViewDipToImage(e.GetPosition(ContentRoot), view);
        var pixel = CoordinateMapper.ImageToPixel(point);
        CursorText.Text = _session.Document.Bounds.Contains(pixel)
            ? Strings.Format("Editor_CursorPosition", pixel.X, pixel.Y)
            : string.Empty;
        if (_session.IsDragging)
        {
            _session.PointerMove(point);
        }
    }

    private void ContentRoot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_panStart is not null)
        {
            EndPan();
            return;
        }

        // 先完成拖曳再釋放捕捉，讓失去捕捉的處理知道這是正常結束。
        if (_session?.IsDragging == true && CurrentTransform() is { } view)
        {
            _session.PointerUp(CoordinateMapper.ViewDipToImage(e.GetPosition(ContentRoot), view));
        }

        if (ContentRoot.IsMouseCaptured)
        {
            ContentRoot.ReleaseMouseCapture();
        }
    }

    private void ContentRoot_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && _session is not null && _panStart is null && !_session.IsDragging)
        {
            e.Handled = true;
            BeginPan(e);
        }
    }

    private void ContentRoot_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && _panStart is not null)
        {
            e.Handled = true;
            EndPan();
        }
    }

    private void ContentRoot_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_session is null || _session.IsDragging || _panStart is not null)
        {
            return;
        }

        CommitTextEdit();
        if (CurrentTransform() is not { } view)
        {
            return;
        }

        e.Handled = true;
        var point = CoordinateMapper.ViewDipToImage(e.GetPosition(ContentRoot), view);
        var menu = PrepareContextMenu(point, CoordinateMapper.ViewLengthToImage(HitToleranceDip, view));
        menu.PlacementTarget = ContentRoot;
        menu.IsOpen = true;
    }

    /// <summary>
    /// 依右鍵的位置（圖片座標）與目前工具選擇快顯選單：裁切工具中為裁切選單；點到標註時選取它並使用標註選單；
    /// 其他位置比照左鍵取消選取並使用畫布選單。項目的可用與顯示狀態在開啟前依目前的文件一次設定好。
    /// </summary>
    internal ContextMenu PrepareContextMenu(Point point, double tolerance)
    {
        var session = _session!;
        if (session.Tool == EditorTool.Crop)
        {
            var cropMenu = (ContextMenu)Resources["CropMenu"];
            MenuItemNamed(cropMenu, "CropResetCropMenuItem").IsEnabled = session.CanResetCrop;
            return cropMenu;
        }

        var hit = session.HitTest(point, tolerance);
        session.Select(hit);
        if (hit is null)
        {
            var canvasMenu = (ContextMenu)Resources["CanvasMenu"];
            MenuItemNamed(canvasMenu, "UndoMenuItem").IsEnabled = session.CanUndo;
            MenuItemNamed(canvasMenu, "RedoMenuItem").IsEnabled = session.CanRedo;
            MenuItemNamed(canvasMenu, "CopyMenuItem").IsEnabled = !_exporting;
            MenuItemNamed(canvasMenu, "SaveMenuItem").IsEnabled = !_exporting;
            MenuItemNamed(canvasMenu, "CanvasResetCropMenuItem").IsEnabled = session.CanResetCrop;
            MenuItemNamed(canvasMenu, "ClearAnnotationsMenuItem").IsEnabled = session.CanClearAnnotations;
            return canvasMenu;
        }

        // 只適用於特定種類的項目只在該種類上顯示。
        var annotationMenu = (ContextMenu)Resources["AnnotationMenu"];
        MenuItemNamed(annotationMenu, "EditTextMenuItem").Visibility = VisibleWhen(hit is TextAnnotation);
        MenuItemNamed(annotationMenu, "EditStepNumberMenuItem").Visibility = VisibleWhen(hit is StepNumberAnnotation);
        MenuItemNamed(annotationMenu, "ReverseArrowMenuItem").Visibility = VisibleWhen(hit is ArrowAnnotation);
        var renumber = MenuItemNamed(annotationMenu, "RenumberStepsMenuItem");
        renumber.Visibility = VisibleWhen(hit is StepNumberAnnotation);
        renumber.IsEnabled = session.CanRenumberSteps;
        return annotationMenu;
    }

    /// <summary>資源中的選單不會產生具名欄位（只設定項目的 Name），以名稱在選單的邏輯樹中找到項目。</summary>
    internal static MenuItem MenuItemNamed(ContextMenu menu, string name) => (MenuItem)LogicalTreeHelper.FindLogicalNode(menu, name);

    private static Visibility VisibleWhen(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private void ContentRoot_MouseLeave(object sender, MouseEventArgs e) => CursorText.Text = string.Empty;

    private void ContentRoot_LostMouseCapture(object sender, MouseEventArgs e)
    {
        // 拖曳途中被其他視窗奪走滑鼠捕捉時放棄這次拖曳，不產生任何命令。
        _session?.AbortDrag();
        if (_panStart is not null)
        {
            _panStart = null;
            UpdateCursor();
        }
    }

    private void ContentRoot_KeyDown(object sender, KeyEventArgs e)
    {
        // 只在畫布本身有焦點時把空白鍵當作平移；文字輸入框中的空白鍵仍是輸入。
        if (e.Key == Key.Space && e.OriginalSource == ContentRoot)
        {
            e.Handled = true;
            if (!_spacePressed)
            {
                _spacePressed = true;
                UpdateCursor();
            }
        }
    }

    private void ContentRoot_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && _spacePressed)
        {
            e.Handled = true;
            _spacePressed = false;
            UpdateCursor();
        }
    }

    private void ContentRoot_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_spacePressed)
        {
            _spacePressed = false;
            UpdateCursor();
        }
    }

    private void BeginPan(MouseEventArgs e)
    {
        _panStart = e.GetPosition(Viewport);
        _panStartOffset = new Point(Viewport.HorizontalOffset, Viewport.VerticalOffset);
        ContentRoot.CaptureMouse();
        UpdateCursor();
    }

    private void EndPan()
    {
        _panStart = null;
        if (ContentRoot.IsMouseCaptured)
        {
            ContentRoot.ReleaseMouseCapture();
        }

        UpdateCursor();
    }

    private void UpdateCursor() =>
        ContentRoot.Cursor = _panStart is not null || _spacePressed
            ? Cursors.Hand
            : _session?.Tool switch
            {
                null or EditorTool.Select => null,
                EditorTool.Text => Cursors.IBeam,
                _ => Cursors.Cross,
            };

    private void CancelOperation()
    {
        _session?.CancelOperation();
        if (ContentRoot.IsMouseCaptured && _panStart is null)
        {
            ContentRoot.ReleaseMouseCapture();
        }
    }

    /// <summary>複製合成後的圖片；成功時記錄匯出版本並回傳 true，失敗時顯示原因、文件不變。</summary>
    private async Task<bool> CopyImageAsync()
    {
        if (_session is null || _exporting)
        {
            return false;
        }

        CommitTextEdit();
        var document = _session.Document;
        var version = document.History.Version;
        var image = ImageRenderer.Render(document);
        SetExporting(true);
        try
        {
            // 合成需要 UI 執行緒；耗時的 PNG 編碼在背景執行緒進行，寫入剪貼簿再回到 UI 執行緒。
            var payload = await Task.Run(() => ClipboardPayload.Create(image));
            if (!ClipboardService.TrySetImage(payload, out var errorCode))
            {
                ShowError(ClipboardService.DescribeError(errorCode));
                return false;
            }
        }
        finally
        {
            SetExporting(false);
        }

        document.MarkExported(version);
        ShowMessage(Strings.Format("Editor_Copied", image.PixelWidth, image.PixelHeight));
        return true;
    }

    /// <summary>另存 PNG；成功時記錄匯出版本與資料夾並回傳 true，取消或失敗時回傳 false、文件不變。</summary>
    private async Task<bool> SaveImageAsync()
    {
        if (_session is null || _exporting)
        {
            return false;
        }

        CommitTextEdit();
        var dialog = new SaveFileDialog
        {
            Title = Strings.Get("Editor_SaveTitle"),
            Filter = Strings.Get("Editor_SaveFilter"),
            DefaultExt = ".png",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = PngFileService.DefaultFileName(DateTime.Now),
            InitialDirectory = InitialSaveDirectory(),
        };
        if (dialog.ShowDialog(this) != true)
        {
            return false;
        }

        // 儲存的是此刻的內容；編碼期間的修改會讓文件再次成為未匯出狀態。
        var document = _session.Document;
        var version = document.History.Version;
        var image = ImageRenderer.Render(document);
        var path = dialog.FileName;
        SetExporting(true);
        try
        {
            await Task.Run(() => PngFileService.Save(image, path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or SecurityException)
        {
            // 寫入失敗時文件仍完整保留在編輯器中，使用者可改存到其他位置。
            ShowError(Strings.Format("Editor_SaveFailed", ErrorText.Describe(ex)));
            return false;
        }
        finally
        {
            SetExporting(false);
        }

        document.MarkExported(version);
        var remembered = _controller.SaveLastSaveDirectory(Path.GetDirectoryName(path)!);
        ShowMessage(remembered.Succeeded
            ? Strings.Format("Editor_Saved", path)
            : Strings.Format("Editor_SavedDirectoryNotRemembered", path, remembered.ErrorMessage));
        return true;
    }

    // 上次成功儲存的資料夾仍存在時從那裡開始，否則從「圖片」資料夾開始。
    private string InitialSaveDirectory() =>
        _controller.Settings.LastSaveDirectory is { } directory && Directory.Exists(directory)
            ? directory
            : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    private void SetExporting(bool exporting)
    {
        _exporting = exporting;
        CommandManager.InvalidateRequerySuggested();
    }

    // 背景編碼期間編輯器可能已被關閉；關閉的視窗不能作為對話框的擁有者。
    private void ShowError(string message)
    {
        if (IsVisible)
        {
            MessageBox.Show(this, message, Strings.Get("Error_Title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            MessageBox.Show(message, Strings.Get("Error_Title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowMessage(string message)
    {
        _message = message;
        UpdateMessage();
        _messageTimer.Stop();
        _messageTimer.Start();
    }

    // 狀態列訊息：最近一次操作結果優先，數秒後消失；裁切工具中持續顯示操作提示。
    private void UpdateMessage() =>
        MessageText.Text = _message ?? (_session?.Tool == EditorTool.Crop ? Strings.Get("Editor_CropHint") : string.Empty);

    private void BringToFront_Click(object sender, RoutedEventArgs e) => _session?.Reorder(ZOrderChange.BringToFront);

    private void BringForward_Click(object sender, RoutedEventArgs e) => _session?.Reorder(ZOrderChange.BringForward);

    private void SendBackward_Click(object sender, RoutedEventArgs e) => _session?.Reorder(ZOrderChange.SendBackward);

    private void SendToBack_Click(object sender, RoutedEventArgs e) => _session?.Reorder(ZOrderChange.SendToBack);

    private void Delete_Click(object sender, RoutedEventArgs e) => _session?.DeleteSelected();

    // 選單項目的 Click 在選單關閉之後才執行，文字輸入框或編號欄位可以取得焦點。
    private void EditSelected_Click(object sender, RoutedEventArgs e) => _session?.EditSelected();

    private void Duplicate_Click(object sender, RoutedEventArgs e) => _session?.Duplicate();

    private void ReverseArrow_Click(object sender, RoutedEventArgs e) => _session?.ReverseArrow();

    private void RenumberSteps_Click(object sender, RoutedEventArgs e) => _session?.RenumberSteps();

    private void Undo_Click(object sender, RoutedEventArgs e) => _session?.Undo();

    private void Redo_Click(object sender, RoutedEventArgs e) => _session?.Redo();

    private async void Copy_Click(object sender, RoutedEventArgs e) => await CopyImageAsync();

    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveImageAsync();

    private void ZoomFit_Click(object sender, RoutedEventArgs e) => ApplyFit();

    private void ZoomActual_Click(object sender, RoutedEventArgs e) => SetZoom(1, fit: false);

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(EditorZoom.ZoomIn(_zoom), fit: false);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(EditorZoom.ZoomOut(_zoom), fit: false);

    private void ResetCrop_Click(object sender, RoutedEventArgs e) => _session?.ResetCrop();

    private void ClearAnnotations_Click(object sender, RoutedEventArgs e) => _session?.ClearAnnotations();

    private void ApplyCrop_Click(object sender, RoutedEventArgs e) => _session?.ApplyCrop();

    private void CancelCrop_Click(object sender, RoutedEventArgs e) => CancelOperation();

    /// <summary>縮放選單的項目；<see cref="Zoom"/> 為 null 代表符合視窗。</summary>
    private sealed record ZoomChoice(double? Zoom, string Label)
    {
        public override string ToString() => Label;
    }
}
