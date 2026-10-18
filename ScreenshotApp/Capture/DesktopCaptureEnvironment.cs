using System.Windows;
using System.Windows.Interop;
using ScreenshotApp.Platform;

namespace ScreenshotApp.Capture;

/// <summary>截圖流程的實際平台步驟：自家 WPF 視窗、GDI 擷取與選取覆蓋層。只在 UI 執行緒使用。</summary>
internal sealed class DesktopCaptureEnvironment : ICaptureEnvironment
{
    public IDisposable HideOwnWindows()
    {
        var hidden = Application.Current.Windows.Cast<Window>().Where(window => window.IsVisible).ToList();
        foreach (var window in hidden)
        {
            // Windows 隱藏視窗時會播放淡出動畫，關閉後隱藏才會在下一個畫面立即生效，不會在半透明的途中被截入。
            SetTransitionsDisabled(window, true);
            window.Hide();
        }

        // 每次 DwmFlush 等待桌面合成器送出下一個畫面；等兩次確保畫面上已沒有剛隱藏的視窗或剛關閉的系統匣選單。
        NativeMethods.DwmFlush();
        NativeMethods.DwmFlush();
        return new HiddenWindows(hidden);
    }

    public DesktopSnapshot TakeSnapshot() =>
        new(MonitorService.GetMonitors()
            .Select(monitor => new MonitorSnapshot(monitor, CaptureService.Capture(monitor.Bounds)))
            .ToList());

    public Task<SelectionResult?> SelectRegionAsync(DesktopSnapshot snapshot) => SelectionController.SelectAsync(snapshot);

    private static void SetTransitionsDisabled(Window window, bool disabled)
    {
        var value = disabled ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(
            new WindowInteropHelper(window).Handle, NativeMethods.DWMWA_TRANSITIONS_FORCEDISABLED, ref value, sizeof(int));
    }

    private sealed class HiddenWindows(List<Window> windows) : IDisposable
    {
        public void Dispose()
        {
            foreach (var window in windows)
            {
                window.Show();
                SetTransitionsDisabled(window, false);
            }
        }
    }
}
