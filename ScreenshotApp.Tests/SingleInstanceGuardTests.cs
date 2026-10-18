using System.Security.AccessControl;
using System.Security.Principal;
using ScreenshotApp.Platform;

namespace ScreenshotApp.Tests;

/// <summary>以每個測試專用的具名物件驗證，不影響正在執行的應用程式。</summary>
public class SingleInstanceGuardTests
{
    private readonly string _mutexName = $@"Local\ScreenshotApp.Tests-{Guid.NewGuid():N}";

    private string ActivationEventName => _mutexName + "-Activate";

    [Fact]
    public void SecondAcquire_ReturnsNullAndWakesTheFirstInstance()
    {
        using var first = SingleInstanceGuard.TryAcquire(_mutexName, ActivationEventName);
        Assert.NotNull(first);
        using var activated = new ManualResetEventSlim();
        first.StartListening(activated.Set);

        var second = SingleInstanceGuard.TryAcquire(_mutexName, ActivationEventName);

        Assert.Null(second);
        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void ActivationBeforeListening_IsDeliveredOnceListeningStarts()
    {
        using var first = SingleInstanceGuard.TryAcquire(_mutexName, ActivationEventName);
        Assert.NotNull(first);
        Assert.Null(SingleInstanceGuard.TryAcquire(_mutexName, ActivationEventName));
        using var activated = new ManualResetEventSlim();

        first.StartListening(activated.Set);

        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void ExistingInstanceWhoseMutexCannotBeOpened_IsTreatedAsRunning()
    {
        // 模擬以系統管理員身分執行的既有執行個體：其具名物件無法由目前權限開啟。
        var security = new MutexSecurity();
        security.AddAccessRule(new MutexAccessRule(WindowsIdentity.GetCurrent().User!, MutexRights.FullControl, AccessControlType.Deny));
        using var foreign = MutexAcl.Create(initiallyOwned: false, _mutexName, out var createdNew, security);
        Assert.True(createdNew);

        Assert.Null(SingleInstanceGuard.TryAcquire(_mutexName, ActivationEventName));
    }

    [Fact]
    public void AfterTheFirstInstanceExits_TheNextAcquireSucceeds()
    {
        var first = SingleInstanceGuard.TryAcquire(_mutexName, ActivationEventName);
        Assert.NotNull(first);
        first.Dispose();

        using var next = SingleInstanceGuard.TryAcquire(_mutexName, ActivationEventName);

        Assert.NotNull(next);
    }
}
