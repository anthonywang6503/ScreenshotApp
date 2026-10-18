using System.Windows.Threading;
using ScreenshotApp.Platform;

namespace ScreenshotApp;

/// <summary>
/// 統一處理未攔截的例外：UI 執行緒的例外記錄並告知使用者後繼續執行，常駐程式不因單一錯誤結束；
/// 背景工作與處理程序層級的例外只能記錄。
/// </summary>
internal sealed class GlobalExceptionHandler
{
    private readonly ErrorLog _log;
    private readonly Action<Exception> _notifyUser;
    private bool _isNotifying;

    public GlobalExceptionHandler(ErrorLog log, Action<Exception> notifyUser)
    {
        _log = log;
        _notifyUser = notifyUser;
    }

    public void Attach(Dispatcher dispatcher)
    {
        dispatcher.UnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnProcessUnhandledException;
    }

    public void Detach(Dispatcher dispatcher)
    {
        dispatcher.UnhandledException -= OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnProcessUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log.Write("UI 執行緒未處理的例外", e.Exception);
        e.Handled = true;

        // 通知期間（例如訊息方塊開啟中）再發生的例外只記錄，避免連續跳出視窗。
        if (_isNotifying)
        {
            return;
        }

        _isNotifying = true;
        try
        {
            _notifyUser(e.Exception);
        }
        finally
        {
            _isNotifying = false;
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _log.Write("背景工作未觀察的例外", e.Exception);
        e.SetObserved();
    }

    private void OnProcessUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        _log.Write("處理程序因未處理的例外結束", e.ExceptionObject as Exception);
}
