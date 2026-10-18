using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Editor.Commands;

/// <summary>新增標註到最上層。</summary>
internal sealed class AddAnnotationCommand(ImageDocument document, Annotation annotation) : IEditCommand
{
    public void Execute()
    {
        if (document.IndexOf(annotation.Id) < 0)
        {
            document.Insert(document.Annotations.Count, annotation);
        }
    }

    public void Undo()
    {
        var index = document.IndexOf(annotation.Id);
        if (index >= 0)
        {
            document.RemoveAt(index);
        }
    }
}

/// <summary>刪除標註；復原時放回原本的層序位置。</summary>
internal sealed class RemoveAnnotationCommand(ImageDocument document, Annotation annotation) : IEditCommand
{
    private int _index = -1;

    public void Execute()
    {
        var index = document.IndexOf(annotation.Id);
        if (index >= 0)
        {
            _index = index;
            document.RemoveAt(index);
        }
    }

    public void Undo()
    {
        if (_index >= 0 && document.IndexOf(annotation.Id) < 0)
        {
            document.Insert(Math.Min(_index, document.Annotations.Count), annotation);
        }
    }
}

/// <summary>把標註換成新值：移動、調整大小或端點、修改樣式、文字與編號都是這個操作。</summary>
internal sealed class ReplaceAnnotationCommand : IEditCommand
{
    private readonly ImageDocument _document;
    private readonly Annotation _before;
    private readonly Annotation _after;

    public ReplaceAnnotationCommand(ImageDocument document, Annotation before, Annotation after)
    {
        if (before.Id != after.Id)
        {
            throw new ArgumentException("替換前後必須是同一個標註。", nameof(after));
        }

        _document = document;
        _before = before;
        _after = after;
    }

    public void Execute() => _document.Replace(_after);

    public void Undo() => _document.Replace(_before);
}

/// <summary>把標註移到指定的層序位置（0 為最底層）。</summary>
internal sealed class ReorderAnnotationCommand(ImageDocument document, Guid id, int fromIndex, int toIndex) : IEditCommand
{
    public void Execute() => document.Move(id, toIndex);

    public void Undo() => document.Move(id, fromIndex);
}

/// <summary>設定裁切範圍；只改變輸出範圍，不改寫原始 Bitmap 與標註座標。</summary>
internal sealed class SetCropCommand(ImageDocument document, PixelRect before, PixelRect after) : IEditCommand
{
    public void Execute() => document.SetCropRect(after);

    public void Undo() => document.SetCropRect(before);
}
