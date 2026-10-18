using System.IO;
using System.Windows;
using System.Windows.Media;
using ScreenshotApp.Capture;
using ScreenshotApp.Editor;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;
using ScreenshotApp.Resources;
using ScreenshotApp.Settings;

namespace ScreenshotApp;

internal sealed record SettingsApplyResult(bool Succeeded, IReadOnlyList<string> Messages);

/// <summary>
/// 建立並連接常駐服務：設定、全域快捷鍵、系統匣、開機啟動、截圖流程與唯一的編輯視窗。只在 UI 執行緒上使用。
/// </summary>
internal sealed class AppController : IDisposable
{
    private readonly SettingsStore _store;
    private readonly StartupService _startup;
    private readonly ErrorLog _log;
    private readonly HotkeyService _hotkey;
    private readonly TrayService _tray;
    private readonly AppStateMachine _state = new();
    private readonly CaptureFlow _captureFlow;
    private AppSettings _settings = new();
    private HotkeyRegistrationResult _hotkeyFailure;
    private SettingsWindow? _settingsWindow;
    private EditorWindow? _editor;

    public AppController(SettingsStore store, StartupService startup, ErrorLog log)
    {
        _store = store;
        _startup = startup;
        _log = log;
        _captureFlow = new CaptureFlow(_state, new DesktopCaptureEnvironment());
        _hotkey = new HotkeyService();
        _hotkey.Pressed += (_, _) => StartCapture();
        _tray = new TrayService();
        _tray.CaptureRequested += (_, _) => StartCaptureAfterMenuCloses();
        _tray.SettingsRequested += (_, _) => ShowSettings();
        _tray.ExitRequested += (_, _) => RequestExit();
    }

    public AppSettings Settings => _settings;

    /// <summary>設定中的快捷鍵目前是否已成功註冊。</summary>
    public bool IsHotkeyActive => _hotkey.Current == _settings.Hotkey;

    /// <summary>設定中的快捷鍵最近一次註冊失敗的原因。</summary>
    public string HotkeyFailureReason => _hotkeyFailure.Describe();

    public void Start(SettingsLoadResult loaded)
    {
        _settings = loaded.Settings;
        var notices = new List<string>();

        switch (loaded.Status)
        {
            case SettingsLoadStatus.Corrupt:
                notices.Add(Strings.Format("Notify_SettingsCorrupt", Path.GetFileName(_store.CorruptBackupPath)));
                break;
            case SettingsLoadStatus.ReadFailed:
                notices.Add(Strings.Format("Notify_SettingsReadFailed", loaded.ErrorMessage));
                break;
        }

        if (loaded.InvalidFields.Count > 0)
        {
            var fields = string.Join(Strings.Get("List_Separator"), loaded.InvalidFields.Select(SettingsStore.DescribeField));
            notices.Add(Strings.Format("Notify_SettingsInvalidFields", fields));
        }

        // 讀取失敗時不寫回，以免覆蓋一份只是暫時無法讀取的有效設定檔。
        if (loaded.Status is SettingsLoadStatus.Missing or SettingsLoadStatus.Corrupt || loaded.InvalidFields.Count > 0)
        {
            var save = _store.Save(_settings);
            if (!save.Succeeded)
            {
                notices.Add(Strings.Format("Notify_SettingsSaveFailed", save.ErrorMessage));
            }
        }

        var registration = _hotkey.Register(_settings.Hotkey);
        if (!registration.Succeeded)
        {
            _hotkeyFailure = registration;
            notices.Add(Strings.Format("Notify_HotkeyFailed", _settings.Hotkey, registration.Describe()));
        }

        if (!_startup.TryApply(_settings.StartWithWindows, out var startupError))
        {
            notices.Add(Strings.Format("Notify_StartupSyncFailed", startupError));
        }

        // 系統匣圖示在快捷鍵註冊之後才出現：圖示出現即代表常駐服務已就緒。
        UpdateTrayToolTip();
        _tray.Show();
        if (notices.Count > 0)
        {
            _tray.ShowNotification(
                Strings.Get("App_Name"),
                string.Join(Environment.NewLine, notices),
                registration.Succeeded ? NotificationKind.Warning : NotificationKind.Error);
        }
    }

    /// <summary>第二個執行個體啟動時喚醒：編輯中帶出編輯視窗，否則顯示設定視窗。</summary>
    public void ActivateFromSecondInstance()
    {
        if (_state.Current == AppState.Editing && _editor is not null)
        {
            ShowEditor(_editor);
        }
        else
        {
            ShowSettings();
        }
    }

    public void ShowSettings()
    {
        // 截圖或選取進行中（例如第二個執行個體在此時喚醒）不顯示，以免設定視窗被截入或蓋住覆蓋層。
        if (_state.Current is AppState.Capturing or AppState.Selecting)
        {
            return;
        }

        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(this);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _settingsWindow.Activate();
    }

    /// <summary>
    /// 套用設定並寫入設定檔。快捷鍵或開機啟動無法生效時保留原本的值，並以訊息說明原因。
    /// </summary>
    public SettingsApplyResult ApplySettings(AppSettings proposed)
    {
        var messages = new List<string>();
        var succeeded = true;
        var effective = proposed;

        // 已註冊的組合與要求相同時不需變更；先前註冊失敗時，這裡也會重試。
        if (_hotkey.Current != proposed.Hotkey)
        {
            var registration = _hotkey.Register(proposed.Hotkey);
            if (registration.Succeeded)
            {
                messages.Add(Strings.Format("Settings_HotkeyApplied", proposed.Hotkey));
            }
            else
            {
                succeeded = false;
                messages.Add(Strings.Format("Settings_HotkeyRejected", proposed.Hotkey, registration.Describe(), _settings.Hotkey));
                effective = effective with { Hotkey = _settings.Hotkey };
            }
        }

        if (proposed.StartWithWindows != _settings.StartWithWindows
            && !_startup.TryApply(proposed.StartWithWindows, out var startupError))
        {
            succeeded = false;
            messages.Add(Strings.Format("Settings_StartupFailed", startupError));
            effective = effective with { StartWithWindows = _settings.StartWithWindows };
        }

        _settings = effective;
        var save = _store.Save(_settings);
        if (!save.Succeeded)
        {
            succeeded = false;
            messages.Add(Strings.Format("Settings_SaveFailed", save.ErrorMessage));
        }
        else if (succeeded)
        {
            messages.Add(Strings.Get("Settings_Applied"));
        }

        UpdateTrayToolTip();
        return new SettingsApplyResult(succeeded, messages);
    }

    /// <summary>
    /// 編輯器在沒有選取標註時變更的顏色、線寬與字級，寫入設定成為之後的預設值。
    /// </summary>
    public SettingsSaveResult SaveAnnotationDefaults(Color color, int strokeWidth, int fontSize)
    {
        var updated = _settings with { DefaultColor = color, DefaultStrokeWidth = strokeWidth, DefaultFontSize = fontSize };
        if (updated == _settings)
        {
            return new SettingsSaveResult(true, null);
        }

        _settings = updated;
        return _store.Save(_settings);
    }

    public void Dispose()
    {
        _settingsWindow?.Close();
        _editor?.Close();
        _hotkey.Dispose();
        _tray.Dispose();
    }

    // 延後到系統匣選單的事件處理結束後才關閉，避免在選單事件中途釋放系統匣圖示。
    private static void RequestExit() =>
        Application.Current.Dispatcher.BeginInvoke(new Action(Application.Current.Shutdown));

    // 延後到選單事件處理結束、選單從畫面移除之後才開始，選單才不會被截入。
    private void StartCaptureAfterMenuCloses() =>
        Application.Current.Dispatcher.BeginInvoke(new Action(StartCapture));

    private async void StartCapture()
    {
        var outcome = await _captureFlow.RunAsync();
        switch (outcome.Status)
        {
            case CaptureStatus.Completed:
                OpenEditor(new ImageDocument(outcome.Image!));
                break;
            case CaptureStatus.Failed:
                _log.Write("截圖失敗", outcome.Error);
                _tray.ShowNotification(
                    Strings.Get("App_Name"), Strings.Format("Notify_CaptureFailed", outcome.Error!.Message), NotificationKind.Error);
                break;
        }
    }

    // 只有一個編輯視窗：已開啟時以新文件取代目前的文件。取消或失敗的截圖不會走到這裡，原本的文件與視窗由截圖流程恢復。
    private void OpenEditor(ImageDocument document)
    {
        _state.BeginEditing();
        if (_editor is null)
        {
            var editor = new EditorWindow(this);
            editor.Closed += (_, _) =>
            {
                _editor = null;

                // 結束程式時編輯視窗由 Dispose 關閉，此時可能不在編輯狀態。
                if (_state.Current == AppState.Editing)
                {
                    _state.EndEditing();
                }
            };
            _editor = editor;
        }

        _editor.Load(document);
        ShowEditor(_editor);
    }

    private static void ShowEditor(EditorWindow editor)
    {
        editor.Show();
        if (editor.WindowState == WindowState.Minimized)
        {
            editor.WindowState = WindowState.Normal;
        }

        editor.Activate();
    }

    private void UpdateTrayToolTip() =>
        _tray.SetToolTip(IsHotkeyActive
            ? Strings.Format("Tray_ToolTipHotkeyActive", Strings.Get("App_Name"), _settings.Hotkey)
            : Strings.Format("Tray_ToolTipHotkeyInactive", Strings.Get("App_Name")));
}
