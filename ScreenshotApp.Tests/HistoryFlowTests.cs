using System.Xml.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ScreenshotApp.Editor;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Export;
using ScreenshotApp.History;
using ScreenshotApp.Platform;
using ScreenshotApp.Settings;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

public sealed class HistoryFlowTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private AppController Controller(HistoryStore store) => new(
        new SettingsStore(_temp.Combine("settings.json")),
        new StartupService(Registry.CurrentUser, @"Software\ScreenshotApp.Tests\HistoryFlowTests", "test", _temp.Combine("app.exe")),
        new ErrorLog(_temp.Combine("logs")), store);

    private static T Wait<T>(Task<T> task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { StaRunner.DoEvents(); Thread.Sleep(1); }
        Assert.True(task.IsCompleted);
        return task.GetAwaiter().GetResult();
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    [Fact]
    public void AT15_OnlyCapturesRecorded_DisablingDetachesExistingDocuments() => StaRunner.Run(() =>
    {
        var store = new HistoryStore(_temp.Combine("history"));
        using var controller = Controller(store);
        var openedFile = HistoryTests.Document();
        controller.SaveHistory(openedFile);
        Assert.Empty(Wait(controller.ListHistoryAsync()).Value!);
        var capture = HistoryTests.Document();
        controller.RecordCapture(capture);
        var entry = Assert.Single(Wait(controller.ListHistoryAsync()).Value!);
        capture.History.Execute(new AddAnnotationCommand(capture, HistoryTests.Annotations()[0]));
        controller.SaveHistory(capture);
        Assert.True(capture.IsDirty);
        Assert.Single(Wait(store.LoadAsync(entry.Id)).Value!.Snapshot.Annotations);
        controller.ConfigureHistory(false, 50);
        capture.History.Execute(new AddAnnotationCommand(capture, HistoryTests.Annotations()[1]));
        controller.SaveHistory(capture);
        controller.RecordCapture(HistoryTests.Document());
        controller.ConfigureHistory(true, 50);
        controller.SaveHistory(capture);
        Assert.Single(Wait(controller.ListHistoryAsync()).Value!);
        Assert.Single(Wait(store.LoadAsync(entry.Id)).Value!.Snapshot.Annotations);
    });

    [Fact]
    public void AT15_DisabledHistory_DoesNotCleanOrPruneExistingFiles() => StaRunner.Run(() =>
    {
        var store = new HistoryStore(_temp.Combine("history"));
        var id = HistoryStore.CreateId(DateTimeOffset.UtcNow);
        var directory = Path.Combine(store.Root, id);
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "original.png.tmp");
        File.WriteAllText(file, "existing");
        using var controller = Controller(store);
        controller.ConfigureHistory(false, 1);
        controller.RecordCapture(HistoryTests.Document());
        Assert.True(Wait(controller.DrainHistoryAsync(TimeSpan.FromSeconds(1))));
        Assert.Equal("existing", File.ReadAllText(file));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AT15_ReplaceOrClose_SavesAnnotationsWithoutMarkingExported(bool close) => StaRunner.Run(() =>
    {
        var store = new HistoryStore(_temp.Combine("history"));
        using var controller = Controller(store);
        var window = new EditorWindow(controller);
        var capture = HistoryTests.Document();
        controller.RecordCapture(capture);
        var entry = Assert.Single(Wait(controller.ListHistoryAsync()).Value!);
        window.Load(capture);
        capture.History.Execute(new AddAnnotationCommand(capture, HistoryTests.Annotations()[0]));
        if (close) window.CloseWithoutConfirmation();
        else { window.Load(HistoryTests.Document()); window.CloseWithoutConfirmation(); }
        Assert.Single(Wait(store.LoadAsync(entry.Id)).Value!.Snapshot.Annotations);
        Assert.True(capture.IsDirty);
    });

    [Fact]
    public void AT15_Dispose_WaitsForCloseSnapshot() => StaRunner.Run(() =>
    {
        var store = new HistoryStore(_temp.Combine("history"));
        var controller = Controller(store);
        var window = new EditorWindow(controller);
        SetField(controller, "_editor", window);
        var capture = HistoryTests.Document();
        controller.RecordCapture(capture);
        var entry = Assert.Single(Wait(controller.ListHistoryAsync()).Value!);
        window.Load(capture);
        capture.History.Execute(new AddAnnotationCommand(capture, HistoryTests.Annotations()[0]));
        controller.Dispose();
        Assert.True(store.Pending.IsCompleted);
        Assert.Single(Wait(store.LoadAsync(entry.Id)).Value!.Snapshot.Annotations);
    });

    [Fact]
    public void AT15_ConfirmationCancel_DoesNotReplaceOrLoad() => StaRunner.Run(() =>
    {
        var store = new HistoryStore(_temp.Combine("history"));
        using var controller = Controller(store);
        var asked = 0;
        var result = Wait(controller.LoadHistoryForReplacementAsync("invalid", () => { asked++; return Task.FromResult(false); }));
        Assert.Null(result); Assert.Equal(1, asked); Assert.False(Directory.Exists(store.Root));
    });

    [Theory]
    [InlineData("capturing")]
    [InlineData("selecting")]
    [InlineData("busy")]
    public void AT15_BusyOrCapturing_DoesNotShowOrLoadHistory(string state) => StaRunner.Run(() =>
    {
        using var controller = Controller(new HistoryStore(_temp.Combine("history")));
        var machine = (AppStateMachine)typeof(AppController).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
        if (state == "busy") SetField(controller, "_prompting", true);
        else { machine.TryBeginCapture(); if (state == "selecting") machine.BeginSelecting(); }
        controller.ShowHistory();
        Assert.Null(typeof(AppController).GetField("_historyWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller));
        var asked = false;
        Assert.Null(Wait(controller.LoadHistoryForReplacementAsync("unused", () => { asked = true; return Task.FromResult(true); })));
        Assert.False(asked);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AT15_NewEditsDuringHistoryDecode_AreProtected(bool undo) => StaRunner.Run(() =>
    {
        using var controller = Controller(new HistoryStore(_temp.Combine("history")));
        var window = new EditorWindow(controller);
        var doc = HistoryTests.Document(); window.Load(doc);
        var tcs = new TaskCompletionSource<HistoryLoaded?>();
        var pending = window.LoadHistoryReplacementAsync(() => tcs.Task);
        doc.History.Execute(new AddAnnotationCommand(doc, HistoryTests.Annotations()[0]));
        if (undo) doc.History.Undo();
        var loaded = new HistoryLoaded(HistorySnapshot.Capture(HistoryTests.Document(), DateTimeOffset.UtcNow), false);
        tcs.SetResult(loaded);
        if (undo) Assert.Same(loaded, Wait(pending)); else Assert.Null(Wait(pending));
        Assert.Same(doc, window.CurrentDocument);
        window.CloseWithoutConfirmation();
    });

    [Fact]
    public void AT15_HistoryCopy_UsesComposedPixelsAndInjectedClipboard() => StaRunner.Run(() =>
    {
        var store = new HistoryStore(_temp.Combine("history"));
        using var controller = Controller(store);
        var doc = HistoryTests.AnnotatedDocument();
        var snapshot = HistorySnapshot.Capture(doc, DateTimeOffset.UtcNow);
        var id = HistoryStore.CreateId(snapshot.Created);
        Assert.True(Wait(store.AddAsync(id, snapshot)).Value);
        byte[]? copied = null;
        Assert.Null(Wait(controller.CopyHistoryAsync(id, data =>
        {
            copied = Assert.IsType<MemoryStream>(data.GetData(ClipboardPayload.PngFormat)).ToArray();
        })));
        Assert.Equal(PngFileService.Encode(ImageRenderer.Render(doc)), copied);
        Assert.True(doc.IsDirty);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AT15_RestoredHistory_NoDuplicate_OnlyValidDataCanUpdate(bool originalOnly) => StaRunner.Run(() =>
    {
        var store = new HistoryStore(_temp.Combine("history"));
        using var controller = Controller(store);
        var snapshot = HistorySnapshot.Capture(HistoryTests.Document(), DateTimeOffset.UtcNow);
        var id = HistoryStore.CreateId(snapshot.Created);
        Assert.True(Wait(store.AddAsync(id, snapshot)).Value);
        var loaded = new HistoryLoaded(snapshot, originalOnly);
        var doc = loaded.Snapshot.Restore();
        controller.AssociateHistory(doc, id, loaded);
        doc.History.Execute(new AddAnnotationCommand(doc, HistoryTests.Annotations()[0]));
        controller.SaveHistory(doc);
        Assert.Single(Wait(store.ListAsync()).Value!);
        Assert.Equal(originalOnly ? 0 : 1, Wait(store.LoadAsync(id)).Value!.Snapshot.Annotations.Length);
    });

    [Fact]
    public void AT15_WindowNamesAndDefaults_AreAvailableWithoutShowing() => StaRunner.Run(() =>
    {
        using var controller = Controller(new HistoryStore(_temp.Combine("history")));
        var window = new HistoryWindow(controller);
        foreach (var name in new[] { "EntriesList", "OpenHistoryButton", "CopyHistoryButton", "DeleteHistoryButton", "ClearHistoryButton", "RefreshHistoryButton", "StatusText" })
            Assert.NotNull(window.FindName(name));
        Wait(window.RefreshAsync().ContinueWith(_ => true));
        Assert.False(((Button)window.FindName("OpenHistoryButton")).IsEnabled);
        var settings = new SettingsWindow(controller);
        Assert.True(settings.HistoryEnabledBox.IsChecked);
        Assert.Equal("50", settings.HistoryLimitBox.Text);
        Assert.Equal(controller.HistoryRoot, settings.HistoryPathBox.Text);
        var editor = new EditorWindow(controller);
        Assert.NotNull(editor.FindName("HistoryButton"));
        editor.CloseWithoutConfirmation(); settings.Close(); window.Close();
    });

    [Theory]
    [InlineData("Editor/EditorWindow.xaml")]
    [InlineData("Settings/SettingsWindow.xaml")]
    [InlineData("History/HistoryWindow.xaml")]
    public void HistoryEntrypoints_DoNotDuplicateWindowAccessKeys(string file)
    {
        var root = System.Xml.Linq.XDocument.Load(Path.Combine(RepositoryPaths.AppProject, file)).Root!;
        root.Elements().Where(element => element.Name.LocalName == "Window.Resources").Remove();
        var keys = System.Text.RegularExpressions.Regex.Matches(root.ToString(), @"\{res:Str (\w+)\}")
            .Select(match => match.Groups[1].Value).Distinct();
        var shortcuts = keys.Select(key => ScreenshotApp.Resources.Strings.Get(key))
            .Select(text => System.Text.RegularExpressions.Regex.Match(text, @"\(_([A-Z])\)"))
            .Where(match => match.Success).Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(shortcuts.Length, shortcuts.Distinct().Count());
    }

    [Fact]
    public void AT15_SessionEndingThatDoesNotExit_DoesNotDisableHistoryUpdates() => StaRunner.Run(() =>
    {
        var store = new HistoryStore(_temp.Combine("history"));
        using var controller = Controller(store);
        var window = new EditorWindow(controller);
        SetField(controller, "_editor", window);
        var doc = HistoryTests.Document(); window.Load(doc);
        controller.RecordCapture(doc);
        var entry = Assert.Single(Wait(store.ListAsync()).Value!);
        controller.OnSessionEnding((SessionEndingCancelEventArgs)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SessionEndingCancelEventArgs)));
        // 其他程式可能取消系統的登出要求，此時本程式仍會繼續執行。
        doc.History.Execute(new AddAnnotationCommand(doc, HistoryTests.Annotations()[0]));
        controller.SaveHistory(doc);
        Assert.Single(Wait(store.LoadAsync(entry.Id)).Value!.Snapshot.Annotations);
    });

    [Fact]
    public void AT15_ExitWait_IsBoundedAndDoesNotWaitTwice() => StaRunner.Run(() =>
    {
        var store = new HistoryStore(_temp.Combine("history"));
        using var controller = Controller(store);
        var blocker = new TaskCompletionSource();
        SetField(store, "_tail", blocker.Task);
        try { Assert.False(Wait(controller.DrainHistoryAsync(TimeSpan.FromMilliseconds(10)))); }
        finally { blocker.SetResult(); }
        Assert.Contains("歷史寫入等待逾時", File.ReadAllText(new ErrorLog(_temp.Combine("logs")).CurrentFilePath));
    });

    [Fact]
    public void AT15_DisableCancelsQueuedMaintenance() => StaRunner.Run(() =>
    {
        var store = new HistoryStore(_temp.Combine("history"));
        var directory = Path.Combine(store.Root, HistoryStore.CreateId(DateTimeOffset.UtcNow));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "original.png.tmp");
        File.WriteAllText(file, "partial");
        var blocker = new TaskCompletionSource(); SetField(store, "_tail", blocker.Task);
        var maintenance = store.MaintainAsync();
        store.Configure(false, 50);
        blocker.SetResult();
        Wait(maintenance);
        Assert.Equal("partial", File.ReadAllText(file));
    });

    [Fact]
    public void AT15_DisableCancelsQueuedWorkAndClearFollowsPendingAdd() => StaRunner.Run(() =>
    {
        var store = new HistoryStore(_temp.Combine("history"));
        var blocker = new TaskCompletionSource();
        SetField(store, "_tail", blocker.Task);
        var snapshot = HistorySnapshot.Capture(HistoryTests.Document(), DateTimeOffset.UtcNow);
        var id = HistoryStore.CreateId(snapshot.Created);
        var add = store.AddAsync(id, snapshot);
        store.Configure(false, 50); store.Configure(true, 50);
        blocker.SetResult();
        Assert.False(Wait(add).Value);
        Assert.False(Directory.Exists(store.Root));
        var secondBlocker = new TaskCompletionSource(); SetField(store, "_tail", secondBlocker.Task);
        add = store.AddAsync(id, snapshot);
        var clear = store.ClearAsync();
        secondBlocker.SetResult();
        Assert.True(Wait(add).Value); Assert.True(Wait(clear).Value);
        Assert.Empty(Wait(store.ListAsync()).Value!);
    });
}
