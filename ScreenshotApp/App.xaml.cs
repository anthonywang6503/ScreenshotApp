using System.Windows;
using System.Windows.Markup;
using ScreenshotApp.Platform;
using ScreenshotApp.Resources;
using ScreenshotApp.Settings;

namespace ScreenshotApp;

public partial class App : Application
{
    private SingleInstanceGuard? _instanceGuard;
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceGuard = SingleInstanceGuard.TryAcquire();
        if (_instanceGuard is null)
        {
            Shutdown();
            return;
        }

        // 讓 WPF 為中文字元選用繁體中文字型，避免字型後援選到簡體字形。
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("zh-TW")));

        var log = new ErrorLog(ErrorLog.DefaultDirectory);
        log.DeleteExpiredFiles();
        new GlobalExceptionHandler(
            log,
            exception => ShowError(Strings.Format("Error_Unexpected", exception.Message, log.CurrentFilePath)))
            .Attach(Dispatcher);

        if (!DpiAwareness.IsPerMonitorV2())
        {
            log.Write("DPI 感知模式不是 Per-Monitor V2，多螢幕座標可能不正確。");
        }

        try
        {
            var executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("無法取得執行檔路徑。");
            var store = new SettingsStore(SettingsStore.DefaultFilePath);
            var controller = new AppController(store, new StartupService(executablePath), log);
            _controller = controller;
            controller.Start(store.Load());

            // 使用 BeginInvoke 而非 InvokeAsync：前者的例外會交給全域例外處理，後者會把例外存進工作而被忽略。
            _instanceGuard.StartListening(() => Dispatcher.BeginInvoke(new Action(controller.ActivateFromSecondInstance)));
        }
        catch (Exception ex)
        {
            // 常駐服務無法建立時沒有系統匣可操作，留在背景只會成為看不見的處理程序，因此告知後結束。
            log.Write("啟動失敗", ex);
            ShowError(Strings.Format("Error_StartupFailed", ex.Message, log.CurrentFilePath));
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _instanceGuard?.Dispose();
        base.OnExit(e);
    }

    private static void ShowError(string message) =>
        MessageBox.Show(message, Strings.Get("Error_Title"), MessageBoxButton.OK, MessageBoxImage.Error);
}
