using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;
using ScreenshotApp.Resources;

namespace ScreenshotApp.Capture;

/// <summary>四條獨立 HWND 都在取樣區域之外，中央完全沒有覆蓋視窗。</summary>
internal sealed class ScrollCaptureFrame : IDisposable
{
    private readonly List<HwndSource> _edges = [];
    public event EventHandler? DisplayChanged;

    public ScrollCaptureFrame(PixelRect region)
    {
        try
        {
            foreach (var edge in Edges(region))
            {
                var source = new HwndSource(new HwndSourceParameters(Strings.Get("Scroll_FrameTitle"))
                {
                    PositionX = edge.X, PositionY = edge.Y, Width = edge.Width, Height = edge.Height,
                    WindowStyle = NativeMethods.WS_POPUP,
                    ExtendedWindowStyle = NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_TOOLWINDOW
                        | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT,
                    UsesPerPixelOpacity = true,
                    HwndSourceHook = WndProc,
                });
                _edges.Add(source);
                source.RootVisual = new Border { Background = Brushes.DodgerBlue, IsHitTestVisible = false };
                NativeMethods.ShowWindow(source.Handle, NativeMethods.SW_SHOWNA);
            }
        }
        catch { Dispose(); throw; }
    }

    internal static PixelRect[] Edges(PixelRect r) =>
    [
        new(r.X - 2, r.Y - 2, r.Width + 4, 2), new(r.X - 2, r.Bottom, r.Width + 4, 2),
        new(r.X - 2, r.Y, 2, r.Height), new(r.Right, r.Y, 2, r.Height),
    ];

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message is NativeMethods.WM_DISPLAYCHANGE or NativeMethods.WM_DPICHANGED)
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        return 0;
    }

    public void Dispose()
    {
        foreach (var edge in _edges) edge.Dispose();
        _edges.Clear();
    }
}
