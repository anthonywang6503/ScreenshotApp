namespace ScreenshotApp.Platform;

internal static class DpiAwareness
{
    /// <summary>目前執行緒是否為 Per-Monitor V2 DPI 感知。</summary>
    public static bool IsPerMonitorV2() =>
        NativeMethods.AreDpiAwarenessContextsEqual(
            NativeMethods.GetThreadDpiAwarenessContext(),
            NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
}
