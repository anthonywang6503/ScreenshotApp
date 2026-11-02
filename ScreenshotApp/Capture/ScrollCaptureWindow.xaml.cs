using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;
using ScreenshotApp.Resources;

namespace ScreenshotApp.Capture;

internal partial class ScrollCaptureWindow : Window
{
    private readonly ScrollCaptureSession _session;
    private readonly PixelRect _region;
    private readonly ScrollTarget _target;
    private ScrollInputStamp _input;
    private bool _closed;
    private bool _finished;
    private bool _displayChanged;
    private nint Handle => new WindowInteropHelper(this).Handle;

    internal ScrollCaptureWindow(PixelRect region, ScrollCaptureSession session)
    {
        _region = region;
        _session = session;
        _target = new ScrollTarget(region);
        InitializeComponent();
        Closed += (_, _) => _closed = true;
        UpdatePreview();
        SetStatus(Strings.Get("Scroll_Manual"));
    }

    public static async Task<BitmapSource?> CaptureAsync(PixelRect region)
    {
        if (region.IsEmpty || region.Height > ScrollStitcher.HeightLimit(region.Width))
            throw new CaptureException(Strings.Get("Scroll_TooLarge"));
        NativeMethods.DwmFlush();
        NativeMethods.DwmFlush();
        try
        {
            var first = CaptureService.Capture(region);
            var frame = await Task.Run(() => ScrollFrame.FromBitmap(first));
            var window = new ScrollCaptureWindow(region, new ScrollCaptureSession(frame));
            try { return await window.RunAsync(); }
            finally { window.Close(); }
        }
        catch (OutOfMemoryException) { throw new CaptureException(Strings.Get("Scroll_MemoryFailed")); }
    }

    private async Task<BitmapSource?> RunAsync()
    {
        using var frame = new ScrollCaptureFrame(_region);
        frame.DisplayChanged += (_, _) => _displayChanged = true;
        Show();
        var disable = 1;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_TRANSITIONS_FORCEDISABLED, ref disable, sizeof(int));
        PlacePanel();
        while (!_closed && !_finished && !_session.Complete)
        {
            await Task.Delay(150);
            if (_closed || _finished) break;
            if (_displayChanged) throw new CaptureException(Strings.Get("Capture_DisplayChanged"));
            if (_session.Stitcher.AtLimit)
            {
                SetStatus(Strings.Get("Scroll_Limit"));
                AutomaticButton.IsEnabled = false;
                continue;
            }
            CheckInput();
            var hidden = PanelOverlaps();
            if (hidden)
            {
                Hide();
                NativeMethods.DwmFlush();
                NativeMethods.DwmFlush();
            }
            BitmapSource image;
            try
            {
                if (!_target.IsUnobscured) throw new CaptureException(Strings.Get("Scroll_TargetChanged"));
                if (_session.NeedsScroll)
                {
                    if (_target.TryScroll(Handle, _input))
                    {
                        _session.ScrollSent();
                        // 讓接收端有一整個取樣間隔處理訊息與平滑捲動。
                        continue;
                    }
                    _session.StopAutomatic();
                    SetStatus(Strings.Get("Scroll_SendFailed"));
                }
                image = CaptureService.Capture(_region);
            }
            finally { if (hidden && !_closed) Show(); }

            var sample = await Task.Run(() => ScrollFrame.FromBitmap(image));
            if (_closed || _finished) break;
            CheckInput();
            if (_displayChanged) throw new CaptureException(Strings.Get("Capture_DisplayChanged"));
            var result = _session.Observe(sample);
            if (result is StitchStatus.Appended or StitchStatus.LimitReached) UpdatePreview();
            if (result == StitchStatus.CannotAlign) SetStatus(Strings.Get("Scroll_CannotAlign"));
            else if (result == StitchStatus.Backward) SetStatus(Strings.Get("Scroll_Backward"));
            else if (result == StitchStatus.Appended) SetStatus(_session.Automatic ? Strings.Get("Scroll_Running") : Strings.Get("Scroll_Manual"));
            if (_session.Unstable) SetStatus(Strings.Get("Scroll_Unstable"));
            if (_session.NoScrollResponse) SetStatus(Strings.Get("Scroll_NoResponse"));
            AutomaticButton.Content = _session.Automatic ? Strings.Get("Scroll_Stop") : Strings.Get("Scroll_Automatic");
        }
        if (_closed && !_finished) return null;
        var stitcher = _session.Stitcher;
        return await Task.Run(() =>
        {
            var image = BitmapSource.Create(stitcher.Width, stitcher.Height, 96, 96, PixelFormats.Bgr32, null, stitcher.GetPixels(), stitcher.Width * 4);
            image.Freeze();
            return image;
        });
    }

    private void CheckInput()
    {
        if (_session.Automatic && (!ScrollInputStamp.TryRead(out var input) || input.Interrupts(_input)))
        {
            _session.StopAutomatic();
            SetStatus(Strings.Get("Scroll_Interrupted"));
            AutomaticButton.Content = Strings.Get("Scroll_Automatic");
        }
    }

    private void Automatic_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Automatic)
        {
            _session.StopAutomatic();
            SetStatus(Strings.Get("Scroll_Manual"));
        }
        else if (ScrollInputStamp.TryRead(out _input) && !_input.KeyDown)
        {
            _session.StartAutomatic();
            SetStatus(Strings.Get("Scroll_Running"));
        }
        else SetStatus(Strings.Get("Scroll_ReleaseKeys"));
        AutomaticButton.Content = _session.Automatic ? Strings.Get("Scroll_Stop") : Strings.Get("Scroll_Automatic");
    }

    private void Finish_Click(object sender, RoutedEventArgs e) { _session.StopAutomatic(); _finished = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; Close(); }
    }
    private void SetStatus(string text) => StatusText.Text = text;

    private void UpdatePreview()
    {
        var pixels = _session.Stitcher.GetPreviewPixels(280, 140, out var width, out var height);
        var preview = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, width * 4);
        preview.Freeze();
        PreviewImage.Source = preview;
        HeightText.Text = Strings.Format("Scroll_Size", _session.Stitcher.Width, _session.Stitcher.Height);
    }

    private bool PanelOverlaps() => !TryPanelRect(out var panel) || !panel.Intersect(_region).IsEmpty;

    private bool TryPanelRect(out PixelRect panel)
    {
        var success = NativeMethods.GetWindowRect(Handle, out var rect);
        panel = new PixelRect(rect.Left, rect.Top, Math.Max(0, rect.Right - rect.Left), Math.Max(0, rect.Bottom - rect.Top));
        return success;
    }

    private void PlacePanel()
    {
        if (!TryPanelRect(out var panel)) return;
        var monitor = MonitorService.GetMonitors().FirstOrDefault(m => m.Bounds.Contains(_region));
        if (monitor is null) throw new CaptureException(Strings.Get("Capture_DisplayChanged"));
        var position = PanelPosition(_region, monitor.Bounds, panel.Width, panel.Height);
        NativeMethods.SetWindowPos(Handle, 0, position.X, position.Y, 0, 0,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
    }

    internal static PixelPoint PanelPosition(PixelRect region, PixelRect screen, int width, int height)
    {
        var candidates = new[]
        {
            new PixelRect(region.Right + 12, region.Y, width, height),
            new PixelRect(region.X - width - 12, region.Y, width, height),
            new PixelRect(region.X, region.Bottom + 12, width, height),
            new PixelRect(region.X, region.Y - height - 12, width, height),
        };
        var chosen = candidates.FirstOrDefault(screen.Contains);
        return chosen.IsEmpty ? new PixelPoint(screen.X, screen.Y) : new PixelPoint(chosen.X, chosen.Y);
    }
}
