namespace ScreenshotApp.Platform;

/// <summary>
/// 以具名 Mutex 確保同一登入工作階段只有一個常駐執行個體；
/// 後啟動的執行個體以具名事件喚醒既有執行個體後自行結束。
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    private const string DefaultMutexName = @"Local\ScreenshotApp-6D2F8E41-7C4B-4D3A-9E0F-3B5A1C9D7E21-Instance";
    private const string DefaultActivationEventName = @"Local\ScreenshotApp-6D2F8E41-7C4B-4D3A-9E0F-3B5A1C9D7E21-Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private RegisteredWaitHandle? _registration;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle activation)
    {
        _mutex = mutex;
        _activation = activation;
    }

    /// <summary>成為第一個執行個體時回傳 guard；已有執行個體時喚醒它並回傳 null。</summary>
    public static SingleInstanceGuard? TryAcquire() => TryAcquire(DefaultMutexName, DefaultActivationEventName);

    internal static SingleInstanceGuard? TryAcquire(string mutexName, string activationEventName)
    {
        try
        {
            // 只用具名物件是否已存在來判斷，不持有 Mutex 的擁有權，因此不受執行緒親和性限制。
            var mutex = new Mutex(initiallyOwned: false, mutexName, out var createdNew);
            if (createdNew)
            {
                return new SingleInstanceGuard(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, activationEventName));
            }

            mutex.Dispose();

            // 剛由使用者啟動的處理程序擁有前景權限；先讓出，既有執行個體才能把視窗帶到前景。
            NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
            using var activation = new EventWaitHandle(false, EventResetMode.AutoReset, activationEventName);
            activation.Set();
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // 既有執行個體以系統管理員身分執行時，一般權限無法開啟它建立的具名物件；
            // 仍視為已有執行個體（只是無法喚醒它），以免產生第二組系統匣圖示與快捷鍵。
            return null;
        }
    }

    /// <summary>開始等待喚醒訊號；回呼在執行緒集區執行，呼叫端需自行切換到 UI 執行緒。</summary>
    public void StartListening(Action onActivationRequested)
    {
        _registration ??= ThreadPool.RegisterWaitForSingleObject(
            _activation, (_, _) => onActivationRequested(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activation.Dispose();
        _mutex.Dispose();
    }
}
