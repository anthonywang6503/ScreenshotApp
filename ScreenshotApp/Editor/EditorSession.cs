using System.Windows;
using System.Windows.Media;
using ScreenshotApp.Capture;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Editor;

internal enum EditorTool
{
    Select,
    Crop,
    Rectangle,
    Ellipse,
    Line,
    Arrow,
    Text,
    StepNumber,
}

internal enum ZOrderChange
{
    BringToFront,
    BringForward,
    SendBackward,
    SendToBack,
}

/// <summary>新增標註使用的樣式；線寬與字級為圖片像素。</summary>
internal sealed record AnnotationStyle(Color Color, int StrokeWidth, string FontFamily, int FontSize);

/// <summary>進行中的文字輸入；<see cref="Original"/> 為重新編輯的既有標註，新文字時為 null。</summary>
internal sealed record TextEdit(TextAnnotation? Original, PixelPoint Origin, Color Color, string FontFamily, int FontSize);

/// <summary>
/// 一份文件的編輯工作階段：目前工具、選取、拖曳預覽、待套用的裁切範圍與文字輸入狀態，以及把指標操作轉成命令的工具邏輯。
/// 指標位置與容差一律為圖片像素（由畫布經 <see cref="CoordinateMapper"/> 換算），因此與畫布縮放無關，也不依賴任何 WPF 元素。
/// 拖曳期間只更新預覽，放開時才執行一筆命令；指標位置限制在原始圖片之內。只在 UI 執行緒使用。
/// </summary>
internal sealed class EditorSession
{
    private Drag? _drag;
    private Guid? _selectedId;
    private PixelRect? _pendingCrop;

    public EditorSession(ImageDocument document, AnnotationStyle style)
    {
        Document = document;
        Style = style;
    }

    /// <summary>文件、選取、預覽或工具狀態改變，畫面需要更新。</summary>
    public event EventHandler? Changed;

    /// <summary>沒有選取標註時變更了樣式，成為之後新增標註的預設值。</summary>
    public event EventHandler? StyleDefaultsChanged;

    /// <summary>雙擊步驟編號，要求編輯它的數值。</summary>
    public event EventHandler? StepNumberEditRequested;

    public ImageDocument Document { get; }

    public EditorTool Tool { get; private set; } = EditorTool.Select;

    public AnnotationStyle Style { get; private set; }

    public TextEdit? TextEdit { get; private set; }

    public bool IsDragging => _drag is not null;

    public bool CanUndo => !IsBusy && Document.History.CanUndo;

    public bool CanRedo => !IsBusy && Document.History.CanRedo;

    /// <summary>目前選取的標註（文件中的版本）。</summary>
    public Annotation? Selected => _selectedId is { } id ? Document.Find(id) : null;

    /// <summary>選取外框與控制點依據的標註：拖曳選取的標註時為預覽，否則為文件中的版本。</summary>
    public Annotation? SelectionDisplay => _drag?.Preview is { } preview && preview.Id == _selectedId ? preview : Selected;

    /// <summary>裁切工具中待套用的裁切範圍（拖曳中為預覽）；其他工具時為 null。</summary>
    public PixelRect? PendingCrop => (_drag as CropDrag)?.Rect ?? _pendingCrop;

    /// <summary>畫面上依層序顯示的標註：拖曳中的標註以預覽取代、新建中的標註在最上層，重新編輯中的文字由輸入框顯示而不在其中。</summary>
    public IEnumerable<Annotation> VisibleAnnotations
    {
        get
        {
            var preview = _drag?.Preview;
            var editingId = TextEdit?.Original?.Id;
            var previewIsNew = preview is not null;
            foreach (var annotation in Document.Annotations)
            {
                if (annotation.Id == editingId)
                {
                    continue;
                }

                if (preview is not null && annotation.Id == preview.Id)
                {
                    previewIsNew = false;
                    yield return preview;
                }
                else
                {
                    yield return annotation;
                }
            }

            if (previewIsNew)
            {
                yield return preview!;
            }
        }
    }

    private bool IsBusy => _drag is not null || TextEdit is not null;

    /// <summary>切換工具。文字輸入中不切換（呼叫端應先提交或取消）；進入裁切工具時以目前的裁切範圍開始，離開時捨棄未套用的變更。</summary>
    public void SetTool(EditorTool tool)
    {
        if (TextEdit is not null || tool == Tool)
        {
            return;
        }

        _drag = null;
        if (tool == EditorTool.Crop)
        {
            _selectedId = null;
            _pendingCrop = Document.CropRect;
        }
        else
        {
            _pendingCrop = null;
        }

        Tool = tool;
        OnChanged();
    }

    /// <summary>由上而下找出點到的標註。</summary>
    public Annotation? HitTest(Point point, double tolerance)
    {
        for (var index = Document.Annotations.Count - 1; index >= 0; index--)
        {
            if (Document.Annotations[index].HitTest(point, tolerance))
            {
                return Document.Annotations[index];
            }
        }

        return null;
    }

    public void Select(Annotation? annotation)
    {
        if (IsBusy || annotation?.Id == _selectedId)
        {
            return;
        }

        _selectedId = annotation?.Id;
        OnChanged();
    }

    public void PointerDown(Point point, double tolerance, int clickCount)
    {
        if (IsBusy)
        {
            return;
        }

        var pixel = ToImagePixel(point);
        if (Tool == EditorTool.Crop)
        {
            BeginCropDrag(point, pixel, tolerance, clickCount);
        }
        else if (SelectionDisplay is { } selected && FindHandle(selected.Handles, point, tolerance) is { } handle)
        {
            // 任何工具下都可以直接拖曳選取標註的控制點，例如畫完矩形後立刻調整大小。
            _drag = new ResizeDrag(selected, handle, point);
        }
        else
        {
            BeginToolOperation(point, pixel, tolerance, clickCount);
        }

        OnChanged();
    }

    public void PointerMove(Point point)
    {
        if (_drag is not null && _drag.Update(point, ToImagePixel(point)))
        {
            OnChanged();
        }
    }

    public void PointerUp(Point point)
    {
        if (_drag is not { } drag)
        {
            return;
        }

        drag.Update(point, ToImagePixel(point));
        _drag = null;
        drag.Complete(this);
        OnChanged();
    }

    /// <summary>放棄進行中的拖曳（例如滑鼠捕捉被其他視窗奪走），不產生任何命令。</summary>
    public void AbortDrag()
    {
        if (_drag is not null)
        {
            _drag = null;
            OnChanged();
        }
    }

    /// <summary>Esc：依序取消拖曳、離開裁切工具（捨棄未套用的範圍）或取消選取；沒有可取消的操作時回傳 false。</summary>
    public bool CancelOperation()
    {
        if (TextEdit is not null)
        {
            return false;
        }

        if (_drag is not null)
        {
            AbortDrag();
            return true;
        }

        if (Tool == EditorTool.Crop)
        {
            SetTool(EditorTool.Select);
            return true;
        }

        if (_selectedId is not null)
        {
            _selectedId = null;
            OnChanged();
            return true;
        }

        return false;
    }

    /// <summary>套用待定的裁切範圍並回到選取工具；範圍沒有變更時不產生命令。</summary>
    public bool ApplyCrop()
    {
        if (Tool != EditorTool.Crop || IsBusy || _pendingCrop is not { } rect)
        {
            return false;
        }

        if (rect != Document.CropRect)
        {
            Document.History.Execute(new SetCropCommand(Document, Document.CropRect, rect));
        }

        _pendingCrop = null;
        Tool = EditorTool.Select;
        OnChanged();
        return true;
    }

    public bool DeleteSelected()
    {
        if (IsBusy || Selected is not { } selected)
        {
            return false;
        }

        Document.History.Execute(new RemoveAnnotationCommand(Document, selected));
        _selectedId = null;
        OnChanged();
        return true;
    }

    public bool Undo() => !IsBusy && Document.History.Undo() && AfterHistoryChanged();

    public bool Redo() => !IsBusy && Document.History.Redo() && AfterHistoryChanged();

    public bool Reorder(ZOrderChange change)
    {
        if (IsBusy || Selected is not { } selected)
        {
            return false;
        }

        var from = Document.IndexOf(selected.Id);
        var top = Document.Annotations.Count - 1;
        var to = change switch
        {
            ZOrderChange.BringToFront => top,
            ZOrderChange.BringForward => Math.Min(from + 1, top),
            ZOrderChange.SendBackward => Math.Max(from - 1, 0),
            _ => 0,
        };
        if (to == from)
        {
            return false;
        }

        Document.History.Execute(new ReorderAnnotationCommand(Document, selected.Id, from, to));
        OnChanged();
        return true;
    }

    // 樣式變更的對象：文字輸入中為正在輸入的文字；有選取時為選取的標註（不適用的屬性忽略）；沒有選取時為之後新增標註的預設值。

    public void SetColor(Color color)
    {
        if (TextEdit is { } edit)
        {
            UpdateTextEdit(edit with { Color = color }, Style with { Color = color });
        }
        else if (Selected is { } selected)
        {
            Replace(selected, selected with { Color = color });
        }
        else
        {
            SetDefaults(Style with { Color = color });
        }
    }

    public void SetStrokeWidth(int strokeWidth)
    {
        if (TextEdit is not null)
        {
            return;
        }

        switch (Selected)
        {
            case BoxAnnotation box:
                Replace(box, box with { StrokeWidth = strokeWidth });
                break;
            case SegmentAnnotation segment:
                Replace(segment, segment with { StrokeWidth = strokeWidth });
                break;
            case null:
                SetDefaults(Style with { StrokeWidth = strokeWidth });
                break;
        }
    }

    public void SetFontFamily(string fontFamily)
    {
        if (TextEdit is { } edit)
        {
            UpdateTextEdit(edit with { FontFamily = fontFamily }, Style with { FontFamily = fontFamily });
        }
        else if (Selected is TextAnnotation text)
        {
            Replace(text, text with { FontFamily = fontFamily });
        }
        else if (Selected is null)
        {
            SetDefaults(Style with { FontFamily = fontFamily });
        }
    }

    public void SetFontSize(int fontSize)
    {
        if (TextEdit is { } edit)
        {
            UpdateTextEdit(edit with { FontSize = fontSize }, Style with { FontSize = fontSize });
        }
        else if (Selected is TextAnnotation text)
        {
            Replace(text, text with { FontSize = fontSize });
        }
        else if (Selected is null)
        {
            SetDefaults(Style with { FontSize = fontSize });
        }
    }

    public void SetStepNumber(int number)
    {
        if (!IsBusy && Selected is StepNumberAnnotation step)
        {
            Replace(step, step with { Number = number });
        }
    }

    /// <summary>
    /// 結束文字輸入：新文字非空白時新增；既有文字有變更時替換、清空時刪除；新文字為空白時不建立。至多產生一筆命令。
    /// </summary>
    public void CommitTextEdit(string text)
    {
        if (TextEdit is not { } edit)
        {
            return;
        }

        TextEdit = null;
        var isBlank = string.IsNullOrWhiteSpace(text);
        if (edit.Original is { } original)
        {
            if (isBlank)
            {
                Document.History.Execute(new RemoveAnnotationCommand(Document, original));
                _selectedId = null;
            }
            else
            {
                var updated = original with { Text = text, Color = edit.Color, FontFamily = edit.FontFamily, FontSize = edit.FontSize };
                if (updated != original)
                {
                    Document.History.Execute(new ReplaceAnnotationCommand(Document, original, updated));
                }

                _selectedId = original.Id;
            }
        }
        else if (!isBlank)
        {
            var created = new TextAnnotation
            {
                Origin = edit.Origin,
                Text = text,
                Color = edit.Color,
                FontFamily = edit.FontFamily,
                FontSize = edit.FontSize,
            };
            Document.History.Execute(new AddAnnotationCommand(Document, created));
            _selectedId = created.Id;
        }

        OnChanged();
    }

    public void CancelTextEdit()
    {
        if (TextEdit is not null)
        {
            TextEdit = null;
            OnChanged();
        }
    }

    private void BeginToolOperation(Point point, PixelPoint pixel, double tolerance, int clickCount)
    {
        switch (Tool)
        {
            case EditorTool.Select:
                var hit = HitTest(point, tolerance);
                _selectedId = hit?.Id;
                if (hit is TextAnnotation text && clickCount >= 2)
                {
                    StartTextEdit(text);
                }
                else if (hit is StepNumberAnnotation && clickCount >= 2)
                {
                    StepNumberEditRequested?.Invoke(this, EventArgs.Empty);
                }
                else if (hit is not null)
                {
                    _drag = new MoveDrag(hit, pixel, isNew: false);
                }

                break;
            case EditorTool.Text:
                if (HitTest(point, tolerance) is TextAnnotation existing)
                {
                    StartTextEdit(existing);
                }
                else
                {
                    _selectedId = null;
                    TextEdit = new TextEdit(null, pixel, Style.Color, Style.FontFamily, Style.FontSize);
                }

                break;
            case EditorTool.StepNumber:
                // 按下即放置，放開前可拖曳調整位置；整個過程只新增一筆。
                _selectedId = null;
                var step = new StepNumberAnnotation
                {
                    Color = Style.Color,
                    Center = pixel,
                    Radius = StepNumberAnnotation.DefaultRadius(Style.FontSize),
                    Number = Document.NextStepNumber,
                };
                _drag = new MoveDrag(step, pixel, isNew: true);
                break;
            default:
                _selectedId = null;
                _drag = new CreateShapeDrag(Tool, pixel, Style);
                break;
        }
    }

    // 拖曳控制點調整範圍；其他位置一律框選新的範圍。進入裁切工具時範圍是整張圖片，若在範圍內拖曳是平移，
    // 最常見的「直接框選」就無從開始，因此不提供平移。
    private void BeginCropDrag(Point point, PixelPoint pixel, double tolerance, int clickCount)
    {
        var crop = _pendingCrop ?? Document.CropRect;
        if (clickCount >= 2 && crop.Contains(pixel))
        {
            ApplyCrop();
        }
        else if (FindHandle(BoxHandles.Of(crop), point, tolerance) is { } handle)
        {
            _drag = new CropResizeDrag(crop, handle, point, Document.Bounds);
        }
        else
        {
            _drag = new CropCreateDrag(pixel);
        }
    }

    private void StartTextEdit(TextAnnotation text)
    {
        _selectedId = text.Id;
        TextEdit = new TextEdit(text, text.Origin, text.Color, text.FontFamily, text.FontSize);
    }

    // 新文字還不是既有標註，輸入中變更的樣式同時成為之後的預設值。
    private void UpdateTextEdit(TextEdit edit, AnnotationStyle defaults)
    {
        TextEdit = edit;
        if (edit.Original is null)
        {
            ApplyDefaults(defaults);
        }

        OnChanged();
    }

    private void SetDefaults(AnnotationStyle style)
    {
        if (ApplyDefaults(style))
        {
            OnChanged();
        }
    }

    private bool ApplyDefaults(AnnotationStyle style)
    {
        if (style == Style)
        {
            return false;
        }

        Style = style;
        StyleDefaultsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void Replace(Annotation before, Annotation after)
    {
        if (IsBusy || after == before)
        {
            return;
        }

        Document.History.Execute(new ReplaceAnnotationCommand(Document, before, after));
        OnChanged();
    }

    private bool AfterHistoryChanged()
    {
        if (_selectedId is { } id && Document.Find(id) is null)
        {
            _selectedId = null;
        }

        if (Tool == EditorTool.Crop)
        {
            _pendingCrop = Document.CropRect;
        }

        OnChanged();
        return true;
    }

    private PixelPoint ToImagePixel(Point point) => Document.Bounds.Clamp(CoordinateMapper.ImageToPixel(point));

    private static HandleKind? FindHandle(IReadOnlyList<AnnotationHandle> handles, Point point, double tolerance)
    {
        HandleKind? nearest = null;
        var nearestDistance = tolerance;
        foreach (var handle in handles)
        {
            var distance = (handle.Position - point).Length;
            if (distance <= nearestDistance)
            {
                nearest = handle.Kind;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>一次拖曳：依指標位置更新預覽，放開時提交。</summary>
    private abstract class Drag
    {
        public virtual Annotation? Preview => null;

        /// <summary>
        /// 依指標位置更新；<paramref name="point"/> 為未限制範圍的圖片座標，<paramref name="pixel"/> 為限制在圖片內的所在像素。
        /// 預覽有變化時回傳 true。
        /// </summary>
        public abstract bool Update(Point point, PixelPoint pixel);

        /// <summary>
        /// 控制點拖曳的位移：兩個位置的差四捨五入成整數像素。控制點畫在像素格線上，不能用「所在像素」相減——
        /// 在格線上按下會落在格線右側的像素，且指標像素被限制在圖片內，右、下邊會差一個像素到不了圖片邊緣。
        /// </summary>
        protected static (int Dx, int Dy) HandleOffset(Point press, Point point) =>
            ((int)Math.Round(point.X - press.X, MidpointRounding.AwayFromZero),
             (int)Math.Round(point.Y - press.Y, MidpointRounding.AwayFromZero));

        public abstract void Complete(EditorSession session);
    }

    /// <summary>平移標註；<c>isNew</c> 時標註尚未加入文件（剛放置的步驟編號），放開時新增而非替換。</summary>
    private sealed class MoveDrag(Annotation original, PixelPoint start, bool isNew) : Drag
    {
        private Annotation? _moved;

        public override Annotation Preview => _moved ?? original;

        // 以限制在圖片內的所在像素計算位移，抓住的位置不會離開圖片，標註也就不會整個被拖出圖片外。
        public override bool Update(Point point, PixelPoint pixel)
        {
            var moved = original.Translate(pixel.X - start.X, pixel.Y - start.Y);
            if (moved == Preview)
            {
                return false;
            }

            _moved = moved;
            return true;
        }

        public override void Complete(EditorSession session)
        {
            if (isNew)
            {
                session.Document.History.Execute(new AddAnnotationCommand(session.Document, Preview));
                session._selectedId = Preview.Id;
            }
            else if (Preview != original)
            {
                session.Document.History.Execute(new ReplaceAnnotationCommand(session.Document, original, Preview));
            }
        }
    }

    /// <summary>以拖曳位移調整控制點：只按下不移動時不會改變標註。</summary>
    private sealed class ResizeDrag(Annotation original, HandleKind handle, Point press) : Drag
    {
        private Annotation? _resized;

        public override Annotation Preview => _resized ?? original;

        public override bool Update(Point point, PixelPoint pixel)
        {
            var (dx, dy) = HandleOffset(press, point);
            var resized = original.Resize(handle, dx, dy);
            if (resized == Preview)
            {
                return false;
            }

            _resized = resized;
            return true;
        }

        public override void Complete(EditorSession session)
        {
            if (Preview != original)
            {
                session.Document.History.Execute(new ReplaceAnnotationCommand(session.Document, original, Preview));
            }
        }
    }

    /// <summary>拖曳建立矩形框、橢圓、直線或箭頭；指標沒有離開起點像素時是零尺寸，不建立。</summary>
    private sealed class CreateShapeDrag(EditorTool tool, PixelPoint start, AnnotationStyle style) : Drag
    {
        private readonly Guid _id = Guid.NewGuid();
        private Annotation? _preview;

        public override Annotation? Preview => _preview;

        public override bool Update(Point point, PixelPoint pixel)
        {
            var shape = pixel == start ? null : Create(pixel);
            if (shape == _preview)
            {
                return false;
            }

            _preview = shape;
            return true;
        }

        public override void Complete(EditorSession session)
        {
            if (_preview is not null)
            {
                session.Document.History.Execute(new AddAnnotationCommand(session.Document, _preview));
                session._selectedId = _preview.Id;
            }
        }

        // 矩形範圍與區域選取相同，包含起點與終點兩個像素。
        private Annotation Create(PixelPoint end) => tool switch
        {
            EditorTool.Rectangle => new RectangleAnnotation
            {
                Id = _id, Color = style.Color, Rect = BoxHandles.Spanning(start, end), StrokeWidth = style.StrokeWidth,
            },
            EditorTool.Ellipse => new EllipseAnnotation
            {
                Id = _id, Color = style.Color, Rect = BoxHandles.Spanning(start, end), StrokeWidth = style.StrokeWidth,
            },
            EditorTool.Line => new LineAnnotation
            {
                Id = _id, Color = style.Color, Start = start, End = end, StrokeWidth = style.StrokeWidth,
            },
            _ => new ArrowAnnotation
            {
                Id = _id, Color = style.Color, Start = start, End = end, StrokeWidth = style.StrokeWidth,
            },
        };
    }

    /// <summary>裁切範圍的拖曳；完成時只更新待套用的範圍，套用時才產生命令。</summary>
    private abstract class CropDrag : Drag
    {
        /// <summary>預覽中的裁切範圍；null 表示沿用原本的範圍。</summary>
        public PixelRect? Rect { get; protected set; }

        public override void Complete(EditorSession session)
        {
            if (Rect is { } rect)
            {
                session._pendingCrop = rect;
            }
        }

        protected bool SetRect(PixelRect? rect)
        {
            if (rect == Rect)
            {
                return false;
            }

            Rect = rect;
            return true;
        }
    }

    /// <summary>框選新的裁切範圍（指標已限制在原始圖片之內）；沒有離開起點像素時沿用原本的範圍。</summary>
    private sealed class CropCreateDrag(PixelPoint start) : CropDrag
    {
        public override bool Update(Point point, PixelPoint pixel) => SetRect(pixel == start ? null : BoxHandles.Spanning(start, pixel));
    }

    /// <summary>拖曳裁切範圍的控制點，移動的邊限制在原始圖片之內（拖到圖片外即停在邊緣），至少 1×1。</summary>
    private sealed class CropResizeDrag : CropDrag
    {
        private readonly PixelRect _original;
        private readonly HandleKind _handle;
        private readonly Point _press;
        private readonly PixelRect _bounds;

        public CropResizeDrag(PixelRect original, HandleKind handle, Point press, PixelRect bounds)
        {
            _original = original;
            _handle = handle;
            _press = press;
            _bounds = bounds;
            Rect = original;
        }

        public override bool Update(Point point, PixelPoint pixel)
        {
            var (dx, dy) = HandleOffset(_press, point);
            return SetRect(BoxHandles.Resize(_original, _handle, dx, dy, _bounds));
        }
    }
}
