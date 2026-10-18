using ScreenshotApp.Resources;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ScreenshotApp.Platform;

internal enum NotificationKind
{
    Info,
    Warning,
    Error,
}

/// <summary>系統匣圖示與選單。選單只放已實作的功能。</summary>
internal sealed class TrayService : IDisposable
{
    // 通知區域工具提示與通知內文的長度上限（含結尾字元）。
    private const int MaxToolTipLength = 127;
    private const int MaxNotificationLength = 255;

    private readonly Drawing.Icon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayService()
    {
        _icon = LoadIcon();
        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add(Strings.Get("Tray_StartCapture"), null, (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add(Strings.Get("Tray_OpenImage"), null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add(Strings.Get("Tray_Settings"), null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(Strings.Get("Tray_Exit"), null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            ContextMenuStrip = _menu,
            Text = Strings.Get("App_Name"),
        };
        _notifyIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                SettingsRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public event EventHandler? CaptureRequested;

    public event EventHandler? OpenRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public void Show() => _notifyIcon.Visible = true;

    public void SetToolTip(string text) => _notifyIcon.Text = Truncate(text, MaxToolTipLength);

    /// <summary>
    /// 顯示系統通知。Windows 的專注模式可能抑制通知，重要狀態必須另有持續可見的呈現方式。
    /// </summary>
    public void ShowNotification(string title, string message, NotificationKind kind) =>
        _notifyIcon.ShowBalloonTip(
            5000,
            Truncate(title, MaxNotificationLength),
            Truncate(message, MaxNotificationLength),
            kind switch
            {
                NotificationKind.Info => Forms.ToolTipIcon.Info,
                NotificationKind.Warning => Forms.ToolTipIcon.Warning,
                _ => Forms.ToolTipIcon.Error,
            });

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private static Drawing.Icon LoadIcon()
    {
        using var stream = typeof(TrayService).Assembly.GetManifestResourceStream("ScreenshotApp.app.ico")
            ?? throw new InvalidOperationException("找不到內嵌的應用程式圖示。");
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    private static string Truncate(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength];
}
