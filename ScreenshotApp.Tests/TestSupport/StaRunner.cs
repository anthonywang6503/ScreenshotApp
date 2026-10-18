using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace ScreenshotApp.Tests.TestSupport;

/// <summary>在專用 STA 執行緒執行需要 WPF Dispatcher 或視窗的測試程式碼。</summary>
internal static class StaRunner
{
    public static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("STA 測試逾時。");
        }

        failure?.Throw();
    }

    /// <summary>處理目前執行緒訊息佇列中已排入的視窗訊息與 Dispatcher 工作。</summary>
    public static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
