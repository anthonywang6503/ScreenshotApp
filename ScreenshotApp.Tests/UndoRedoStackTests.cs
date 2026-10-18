using ScreenshotApp.Editor.Commands;

namespace ScreenshotApp.Tests;

/// <summary>
/// 驗證編輯歷史：執行、復原、重做的先後順序，執行新命令時清除可重做的紀錄，超過容量時捨棄最舊的一筆，
/// 以及執行失敗的命令不被記錄。
/// </summary>
public class UndoRedoStackTests
{
    [Fact]
    public void Execute_RunsCommandAndMakesItUndoable()
    {
        var log = new List<string>();
        var stack = new UndoRedoStack();

        stack.Execute(new RecordingCommand("a", log));

        Assert.Equal(new[] { "a.Execute" }, log);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Equal(1, stack.UndoCount);
        Assert.Equal(0, stack.RedoCount);
    }

    [Fact]
    public void UndoAndRedo_FollowLastInFirstOutOrder()
    {
        var log = new List<string>();
        var stack = new UndoRedoStack();
        stack.Execute(new RecordingCommand("a", log));
        stack.Execute(new RecordingCommand("b", log));
        stack.Execute(new RecordingCommand("c", log));
        log.Clear();

        Assert.True(stack.Undo());
        Assert.True(stack.Undo());
        Assert.True(stack.Redo());
        Assert.True(stack.Undo());
        Assert.True(stack.Undo());
        Assert.True(stack.Redo());
        Assert.True(stack.Redo());
        Assert.True(stack.Redo());

        Assert.Equal(new[] { "c.Undo", "b.Undo", "b.Execute", "b.Undo", "a.Undo", "a.Execute", "b.Execute", "c.Execute" }, log);
        Assert.Equal(3, stack.UndoCount);
        Assert.Equal(0, stack.RedoCount);
    }

    [Fact]
    public void Execute_AfterUndo_ClearsRedo()
    {
        var log = new List<string>();
        var stack = new UndoRedoStack();
        stack.Execute(new RecordingCommand("a", log));
        stack.Execute(new RecordingCommand("b", log));
        stack.Undo();

        stack.Execute(new RecordingCommand("c", log));

        Assert.False(stack.CanRedo);
        Assert.Equal(0, stack.RedoCount);
        Assert.False(stack.Redo());
        Assert.Equal(2, stack.UndoCount);
        log.Clear();
        stack.Undo();
        stack.Undo();
        Assert.Equal(new[] { "c.Undo", "a.Undo" }, log); // 被清除的 b 不會再出現
    }

    [Fact]
    public void Execute_BeyondDefaultCapacity_DropsTheOldest()
    {
        var log = new List<string>();
        var stack = new UndoRedoStack();
        for (var i = 1; i <= 101; i++)
        {
            stack.Execute(new RecordingCommand($"#{i}", log));
        }

        Assert.Equal(100, UndoRedoStack.DefaultCapacity);
        Assert.Equal(100, stack.UndoCount);
        for (var i = 0; i < 100; i++)
        {
            Assert.True(stack.Undo());
        }

        Assert.False(stack.Undo()); // 第 101 次復原：最舊的一筆已被捨棄
        Assert.Contains("#2.Undo", log);
        Assert.DoesNotContain("#1.Undo", log);
    }

    [Fact]
    public void Execute_BeyondCustomCapacity_DropsTheOldest()
    {
        var log = new List<string>();
        var stack = new UndoRedoStack(capacity: 2);
        stack.Execute(new RecordingCommand("a", log));
        stack.Execute(new RecordingCommand("b", log));
        stack.Execute(new RecordingCommand("c", log));
        log.Clear();

        Assert.Equal(2, stack.UndoCount);
        Assert.True(stack.Undo());
        Assert.True(stack.Undo());
        Assert.False(stack.Undo());
        Assert.Equal(new[] { "c.Undo", "b.Undo" }, log);
    }

    [Fact]
    public void Execute_CommandThrows_IsNotRecordedAndHistoryIsUnchanged()
    {
        var log = new List<string>();
        var stack = new UndoRedoStack();
        stack.Execute(new RecordingCommand("a", log));
        stack.Execute(new RecordingCommand("b", log));
        stack.Undo();
        log.Clear();

        Assert.Throws<InvalidOperationException>(() => stack.Execute(new FailingCommand()));

        Assert.Equal(1, stack.UndoCount);
        Assert.Equal(1, stack.RedoCount);
        Assert.True(stack.Undo());
        Assert.False(stack.Undo());
        Assert.Equal(new[] { "a.Undo" }, log); // 失敗的命令從未被復原
    }

    [Fact]
    public void UndoAndRedo_WhenEmpty_ReturnFalse()
    {
        var stack = new UndoRedoStack();

        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.False(stack.Undo());
        Assert.False(stack.Redo());
        Assert.Equal(0, stack.UndoCount);
        Assert.Equal(0, stack.RedoCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_CapacityBelowOne_ThrowsArgumentOutOfRange(int capacity)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new UndoRedoStack(capacity));

        Assert.Equal("capacity", exception.ParamName);
    }

    [Fact]
    public void Constructor_CapacityOne_KeepsOnlyTheLatestCommand()
    {
        var log = new List<string>();
        var stack = new UndoRedoStack(capacity: 1);
        stack.Execute(new RecordingCommand("a", log));
        stack.Execute(new RecordingCommand("b", log));
        log.Clear();

        Assert.True(stack.Undo());
        Assert.False(stack.Undo());
        Assert.Equal(new[] { "b.Undo" }, log);
    }

    // 把每次呼叫記錄到共用的清單，用來驗證命令被執行與復原的先後順序。
    private sealed class RecordingCommand(string name, List<string> log) : IEditCommand
    {
        public void Execute() => log.Add($"{name}.Execute");

        public void Undo() => log.Add($"{name}.Undo");
    }

    private sealed class FailingCommand : IEditCommand
    {
        public void Execute() => throw new InvalidOperationException("執行失敗");

        public void Undo() => throw new InvalidOperationException("從未成功執行的命令不應被復原");
    }
}
