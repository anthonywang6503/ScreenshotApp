using System.Runtime.CompilerServices;
using System.Windows;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Export;
using ScreenshotApp.History;
using ScreenshotApp.Platform;
using ScreenshotApp.Resources;

namespace ScreenshotApp;

internal sealed partial class AppController
{
    private sealed record HistoryAssociation(string Id, DateTimeOffset Created);
    private readonly HistoryStore _history;
    private ConditionalWeakTable<ImageDocument, HistoryAssociation> _historyDocuments = new();
    private HistoryWindow? _historyWindow;
    private bool _started;
    private bool _disposed;
    private bool _historyErrorShown;
    private bool _historyShuttingDown;
    private bool _historyDrainAttempted;

    public string HistoryRoot => _history.Root;
    internal bool CanUseHistory => !IsBusy && _state.Current is not (AppState.Capturing or AppState.Selecting);

    internal void ConfigureHistory(bool enabled, int limit)
    {
        _settings = _settings with { HistoryEnabled = enabled, HistoryLimit = limit };
        _history.Configure(_settings.HistoryEnabled, _settings.HistoryLimit);
        if (!_settings.HistoryEnabled) _historyDocuments = new();
        if (_settings.HistoryEnabled) _ = ObserveHistoryAsync(_history.MaintainAsync());
    }

    internal void RecordCapture(ImageDocument document)
    {
        if (!_settings.HistoryEnabled) return;
        var created = DateTimeOffset.UtcNow;
        var id = HistoryStore.CreateId(created);
        _historyDocuments.Add(document, new(id, created));
        _ = ObserveHistoryAsync(_history.AddAsync(id, HistorySnapshot.Capture(document, created)));
    }

    internal void SaveHistory(ImageDocument document)
    {
        if (_historyShuttingDown || !_settings.HistoryEnabled || !_historyDocuments.TryGetValue(document, out var association)) return;
        _ = ObserveHistoryAsync(_history.UpdateAsync(association.Id, HistorySnapshot.Capture(document, association.Created)));
    }

    private async Task ObserveHistoryAsync(Task<HistoryResult<bool>> pending)
    {
        var result = await pending;
        if (result.Error is not null)
        {
            // 不把例外訊息或文件內容寫入日誌。
            _log.Write("歷史磁碟作業失敗");
            if (_started && !_disposed && !_historyErrorShown)
            {
                _historyErrorShown = true;
                _tray.ShowNotification(Strings.Get("History_Title"), Strings.Format("History_Failed", result.Error), NotificationKind.Warning);
            }
        }
        if (result.CreatedRoot && _started && !_disposed)
            _tray.ShowNotification(Strings.Get("History_Title"), Strings.Get("History_FirstUse"), NotificationKind.Info);
        if (!_disposed && _historyWindow is not null) await _historyWindow.RefreshAsync();
    }

    public void ShowHistory()
    {
        if (!CanUseHistory) return;
        if (_historyWindow is null)
        {
            _historyWindow = new HistoryWindow(this);
            _historyWindow.Closed += (_, _) => _historyWindow = null;
            _historyWindow.Show();
        }
        if (_historyWindow.WindowState == WindowState.Minimized) _historyWindow.WindowState = WindowState.Normal;
        _historyWindow.Activate();
    }

    internal Task<HistoryResult<IReadOnlyList<HistoryEntry>>> ListHistoryAsync() => _history.ListAsync();

    internal async Task<HistoryLoaded?> LoadHistoryForReplacementAsync(string id, Func<Task<bool>> confirm)
    {
        if (!CanUseHistory || !await confirm()) return null;
        _prompting = true;
        try
        {
            _editor?.SaveHistory();
            async Task<HistoryLoaded?> Load()
            {
                var result = await _history.LoadAsync(id);
                if (result.Error is not null)
                {
                    if (_started) ShowError(_historyWindow, Strings.Format("History_Failed", result.Error));
                    return null;
                }
                return result.Value;
            }
            return _editor is null ? await Load() : await _editor.LoadHistoryReplacementAsync(Load);
        }
        finally { _prompting = false; }
    }

    internal void AssociateHistory(ImageDocument document, string id, HistoryLoaded loaded)
    {
        if (_settings.HistoryEnabled && !loaded.OriginalOnly)
            _historyDocuments.Add(document, new(id, loaded.Snapshot.Created));
    }

    internal async Task OpenHistoryAsync(string id)
    {
        var loaded = await LoadHistoryForReplacementAsync(id, ConfirmUnexportedChangesAsync);
        if (loaded is null) return;
        var document = loaded.Snapshot.Restore();
        AssociateHistory(document, id, loaded);
        OpenEditor(document);
        if (loaded.OriginalOnly) ShowError(_editor, Strings.Get("History_OriginalOnly"));
    }

    internal async Task<string?> CopyHistoryAsync(string id, Action<System.Windows.DataObject>? setClipboard = null)
    {
        if (!CanUseHistory) return null;
        _prompting = true;
        try
        {
            var loaded = await _history.LoadAsync(id);
            if (loaded.Value is null) return loaded.Error;
            var image = ImageRenderer.Render(loaded.Value.Snapshot.Restore());
            var payload = await Task.Run(() => ClipboardPayload.Create(image));
            var copied = setClipboard is null
                ? ClipboardService.TrySetImage(payload, out var error)
                : ClipboardService.TrySetImage(payload, setClipboard, out error);
            if (!copied) return ClipboardService.DescribeError(error);
            return loaded.Value.OriginalOnly ? Strings.Get("History_OriginalOnly") : null;
        }
        finally { _prompting = false; }
    }

    internal async Task<string?> DeleteHistoryAsync(string id)
    {
        if (!CanUseHistory) return null;
        _prompting = true;
        try { return (await _history.DeleteAsync(id)).Error; }
        finally { _prompting = false; }
    }

    public async Task ClearHistoryAsync(Window owner)
    {
        if (!CanUseHistory) return;
        _prompting = true;
        try
        {
            if (MessageBox.Show(owner, Strings.Get("History_ClearConfirm"), Strings.Get("History_Title"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            var result = await _history.ClearAsync();
            if (result.Error is not null) ShowError(owner, Strings.Format("History_Failed", result.Error));
            if (_historyWindow is not null) await _historyWindow.RefreshAsync();
        }
        finally { _prompting = false; }
    }

    internal async Task<bool> DrainHistoryAsync(TimeSpan timeout)
    {
        _historyShuttingDown = true;
        _historyDrainAttempted = true;
        try
        {
            await _history.Pending.WaitAsync(timeout);
            return true;
        }
        catch (TimeoutException)
        {
            _log.Write("歷史寫入等待逾時");
            return false;
        }
    }
}
