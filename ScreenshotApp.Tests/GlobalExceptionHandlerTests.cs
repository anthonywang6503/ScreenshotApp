using System.Windows.Threading;
using ScreenshotApp.Platform;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

public sealed class GlobalExceptionHandlerTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void DispatcherException_IsLoggedHandledAndLaterWorkStillRuns()
    {
        var log = new ErrorLog(_temp.Combine("logs"));
        var notified = new List<Exception>();
        var laterWorkRan = false;

        StaRunner.Run(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var handler = new GlobalExceptionHandler(log, notified.Add);
            handler.Attach(dispatcher);
            try
            {
                dispatcher.BeginInvoke(new Action(() => throw new InvalidOperationException("第一個例外")));
                dispatcher.BeginInvoke(new Action(() => laterWorkRan = true));
                StaRunner.DoEvents();
            }
            finally
            {
                handler.Detach(dispatcher);
            }
        });

        Assert.True(laterWorkRan);
        var exception = Assert.Single(notified);
        Assert.Equal("第一個例外", exception.Message);
        Assert.Contains("第一個例外", File.ReadAllText(log.CurrentFilePath));
    }

    [Fact]
    public void ExceptionWhileNotifying_IsLoggedWithoutNotifyingAgain()
    {
        var log = new ErrorLog(_temp.Combine("logs"));
        var notifyCount = 0;

        StaRunner.Run(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var handler = new GlobalExceptionHandler(log, _ =>
            {
                // 模擬訊息方塊開啟期間又發生例外。
                notifyCount++;
                dispatcher.BeginInvoke(new Action(() => throw new InvalidOperationException("通知期間的例外")));
                StaRunner.DoEvents();
            });
            handler.Attach(dispatcher);
            try
            {
                dispatcher.BeginInvoke(new Action(() => throw new InvalidOperationException("第一個例外")));
                StaRunner.DoEvents();
            }
            finally
            {
                handler.Detach(dispatcher);
            }
        });

        Assert.Equal(1, notifyCount);
        var text = File.ReadAllText(log.CurrentFilePath);
        Assert.Contains("第一個例外", text);
        Assert.Contains("通知期間的例外", text);
    }
}
