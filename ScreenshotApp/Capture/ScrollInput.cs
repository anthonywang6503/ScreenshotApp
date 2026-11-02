using System.Runtime.InteropServices;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;

namespace ScreenshotApp.Capture;

internal readonly record struct ScrollInputStamp(uint Time, PixelPoint Cursor, bool KeyDown)
{
    public bool Interrupts(ScrollInputStamp baseline) => Time != baseline.Time || Cursor != baseline.Cursor || KeyDown;

    public static bool TryRead(out ScrollInputStamp stamp)
    {
        var info = new NativeMethods.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
        if (!NativeMethods.GetLastInputInfo(ref info) || !NativeMethods.GetCursorPos(out var cursor))
        {
            stamp = default;
            return false;
        }
        var keyDown = false;
        for (var key = 1; key < 256; key++)
            keyDown |= (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0;
        stamp = new ScrollInputStamp(info.dwTime, new PixelPoint(cursor.X, cursor.Y), keyDown);
        return true;
    }
}

/// <summary>只向開始時指定的視窗送出滾輪訊息；不移動游標，不注入全域輸入。</summary>
internal sealed class ScrollTarget
{
    private readonly NativeMethods.POINT _center;
    private readonly nint _window;
    private readonly nint _root;
    private readonly uint _process;

    public ScrollTarget(PixelRect region)
    {
        _center = new NativeMethods.POINT { X = region.X + region.Width / 2, Y = region.Y + region.Height / 2 };
        _window = NativeMethods.WindowFromPoint(_center);
        _root = NativeMethods.GetAncestor(_window, NativeMethods.GA_ROOT);
        NativeMethods.GetWindowThreadProcessId(_window, out _process);
    }

    public bool IsUnobscured => _window != 0 && _process != (uint)Environment.ProcessId
        && NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(_center), NativeMethods.GA_ROOT) == _root
        && NativeMethods.GetWindowThreadProcessId(_window, out var process) != 0 && process == _process;

    public bool TryScroll(nint panel, ScrollInputStamp baseline)
    {
        // WM_MOUSEWHEEL 的桌面座標使用有號 16 位元；超出時不得截斷到另一個位置。
        if (_center.X is < short.MinValue or > short.MaxValue || _center.Y is < short.MinValue or > short.MaxValue
            || !IsUnobscured || !ScrollInputStamp.TryRead(out var input) || input.Interrupts(baseline)) return false;
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground != panel && NativeMethods.GetAncestor(foreground, NativeMethods.GA_ROOT) != _root) return false;
        var position = unchecked((int)(((uint)_center.Y & 0xffff) << 16 | ((uint)_center.X & 0xffff)));
        return NativeMethods.PostMessage(_window, NativeMethods.WM_MOUSEWHEEL, unchecked((nint)(-120 << 16)), position);
    }
}
