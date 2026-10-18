using System.Runtime.InteropServices;
using System.Windows.Interop;
using ScreenshotApp.Resources;
using ScreenshotApp.Settings;

namespace ScreenshotApp.Platform;

internal readonly record struct HotkeyRegistrationResult(bool Succeeded, int ErrorCode)
{
    public static HotkeyRegistrationResult Success { get; } = new(true, 0);

    public static HotkeyRegistrationResult Failure(int errorCode) => new(false, errorCode);

    /// <summary>失敗原因的介面文字。</summary>
    public string Describe() => ErrorCode == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED
        ? Strings.Get("Hotkey_ErrorInUse")
        : Strings.Format("Hotkey_ErrorCode", ErrorCode);
}

/// <summary>
/// 以 RegisterHotKey 註冊全域快捷鍵，WM_HOTKEY 由專用的訊息視窗接收（模態對話框期間也能收到）。
/// 變更組合時先註冊新組合，成功後才解除舊組合；失敗時保留原本的註冊。
/// 需在具有 WPF Dispatcher 的 STA 執行緒上建立與使用。
/// </summary>
internal sealed class HotkeyService : IDisposable
{
    private readonly HwndSource _messageWindow;
    private int _registeredId;
    private bool _disposed;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("ScreenshotApp.Hotkey")
        {
            WindowStyle = 0,
            ParentWindow = NativeMethods.HWND_MESSAGE,
        };
        _messageWindow = new HwndSource(parameters);
        _messageWindow.AddHook(WndProc);
    }

    public event EventHandler? Pressed;

    /// <summary>目前成功註冊的組合；沒有註冊時為 null。</summary>
    public HotkeyGesture? Current { get; private set; }

    internal nint WindowHandle => _messageWindow.Handle;

    internal int RegisteredId => _registeredId;

    public HotkeyRegistrationResult Register(HotkeyGesture gesture)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (gesture.Validate() != HotkeyValidation.Valid)
        {
            throw new ArgumentException("快捷鍵組合未通過驗證。", nameof(gesture));
        }

        if (Current == gesture)
        {
            return HotkeyRegistrationResult.Success;
        }

        // 新舊組合使用不同識別碼，才能在解除舊組合之前先確認新組合可用。
        var id = _registeredId == 1 ? 2 : 1;
        var modifiers = (uint)gesture.Modifiers | NativeMethods.MOD_NOREPEAT;
        if (!NativeMethods.RegisterHotKey(WindowHandle, id, modifiers, (uint)gesture.VirtualKey))
        {
            return HotkeyRegistrationResult.Failure(Marshal.GetLastPInvokeError());
        }

        if (_registeredId != 0)
        {
            NativeMethods.UnregisterHotKey(WindowHandle, _registeredId);
        }

        _registeredId = id;
        Current = gesture;
        return HotkeyRegistrationResult.Success;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_registeredId != 0)
        {
            NativeMethods.UnregisterHotKey(WindowHandle, _registeredId);
            _registeredId = 0;
        }

        Current = null;
        _messageWindow.RemoveHook(WndProc);
        _messageWindow.Dispose();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _registeredId != 0 && wParam == _registeredId)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return 0;
    }
}
