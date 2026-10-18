using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Commands;

namespace ScreenshotApp.Editor.Model;

/// <summary>
/// 一份編輯中的圖片：凍結的原始 Bitmap、非破壞性的裁切範圍、依層序排列的標註（索引 0 為最底層）與編輯歷史。
/// 原始 Bitmap 在整個編輯工作階段中不會被修改；標註與裁切只經由 <see cref="History"/> 執行的命令改變。只在 UI 執行緒使用。
/// </summary>
internal sealed class ImageDocument
{
    private readonly List<Annotation> _annotations = [];

    public ImageDocument(BitmapSource source)
    {
        if (!source.IsFrozen)
        {
            throw new ArgumentException("原始圖片必須已凍結，才能確保編輯期間不被修改。", nameof(source));
        }

        Source = source;
        PixelWidth = source.PixelWidth;
        PixelHeight = source.PixelHeight;
        CropRect = Bounds;
    }

    public BitmapSource Source { get; }

    public int PixelWidth { get; }

    public int PixelHeight { get; }

    /// <summary>整張原始圖片的範圍。</summary>
    public PixelRect Bounds => new(0, 0, PixelWidth, PixelHeight);

    /// <summary>輸出範圍；非空且位於原始圖片之內。</summary>
    public PixelRect CropRect { get; private set; }

    public IReadOnlyList<Annotation> Annotations => _annotations;

    public UndoRedoStack History { get; } = new();

    /// <summary>新步驟編號：現有最大編號加 1。刪除編號不會重排，也不會重複使用比現有最大值小的編號。</summary>
    public int NextStepNumber =>
        _annotations.OfType<StepNumberAnnotation>().Select(step => step.Number).DefaultIfEmpty(0).Max() + 1;

    public int IndexOf(Guid id) => _annotations.FindIndex(annotation => annotation.Id == id);

    public Annotation? Find(Guid id) => _annotations.Find(annotation => annotation.Id == id);

    // 以下修改只供命令使用，確保文件狀態永遠等於歷史紀錄的結果。

    internal void Insert(int index, Annotation annotation)
    {
        if (IndexOf(annotation.Id) >= 0)
        {
            throw new InvalidOperationException("相同 Id 的標註已存在。");
        }

        _annotations.Insert(index, annotation);
    }

    internal void RemoveAt(int index) => _annotations.RemoveAt(index);

    internal void Replace(Annotation annotation) => _annotations[RequireIndex(annotation.Id)] = annotation;

    internal void Move(Guid id, int index)
    {
        var current = RequireIndex(id);
        var annotation = _annotations[current];
        _annotations.RemoveAt(current);
        _annotations.Insert(index, annotation);
    }

    internal void SetCropRect(PixelRect rect)
    {
        if (rect.IsEmpty || !Bounds.Contains(rect))
        {
            throw new ArgumentOutOfRangeException(nameof(rect), rect, "裁切範圍不得為空，且必須位於原始圖片之內。");
        }

        CropRect = rect;
    }

    private int RequireIndex(Guid id)
    {
        var index = IndexOf(id);
        return index >= 0 ? index : throw new InvalidOperationException("找不到指定的標註。");
    }
}
