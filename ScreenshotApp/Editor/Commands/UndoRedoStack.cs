namespace ScreenshotApp.Editor.Commands;

/// <summary>可復原的編輯操作。<see cref="Execute"/> 與 <see cref="Undo"/> 都是把文件設為某個狀態，重複執行無害。</summary>
internal interface IEditCommand
{
    void Execute();

    void Undo();
}

/// <summary>
/// 編輯歷史：執行新命令時清除可重做的紀錄；超過容量時捨棄最舊的一筆。只保存命令，不保存任何 Bitmap。
/// </summary>
internal sealed class UndoRedoStack
{
    public const int DefaultCapacity = 100;

    private readonly int _capacity;
    private readonly LinkedList<IEditCommand> _undo = new();
    private readonly Stack<IEditCommand> _redo = new();

    public UndoRedoStack(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public int UndoCount => _undo.Count;

    public int RedoCount => _redo.Count;

    /// <summary>執行並記錄命令；命令拋出例外時不記錄。</summary>
    public void Execute(IEditCommand command)
    {
        command.Execute();
        _undo.AddLast(command);
        if (_undo.Count > _capacity)
        {
            _undo.RemoveFirst();
        }

        _redo.Clear();
    }

    /// <summary>復原最近一筆；沒有可復原的操作時回傳 false。</summary>
    public bool Undo()
    {
        if (_undo.Last is not { } last)
        {
            return false;
        }

        _undo.RemoveLast();
        last.Value.Undo();
        _redo.Push(last.Value);
        return true;
    }

    /// <summary>重做最近一筆被復原的操作；沒有時回傳 false。</summary>
    public bool Redo()
    {
        if (!_redo.TryPop(out var command))
        {
            return false;
        }

        command.Execute();
        _undo.AddLast(command);
        return true;
    }
}
