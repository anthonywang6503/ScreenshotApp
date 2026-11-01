using System.Windows.Input;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;
using ScreenshotApp.Resources;

namespace ScreenshotApp.Capture;

/// <summary>
/// 為一份桌面快照在每個顯示器顯示覆蓋層，處理拖曳選取與取消，結束時關閉全部覆蓋層。只在 UI 執行緒使用。
/// 滑鼠位置取自每個事件本身並換成桌面實體像素；拖曳期間滑鼠捕捉鎖定在起始覆蓋層，
/// 游標移到其他顯示器時仍由它處理並夾取在起始顯示器內。
/// </summary>
internal sealed class SelectionController
{
    private readonly IReadOnlyList<MonitorInfo> _monitors;
    private readonly List<SelectionOverlay> _overlays = [];

    // 結果以非同步方式交給等待者，覆蓋層的輸入事件處理先完整結束，之後才繼續截圖流程。
    private readonly TaskCompletionSource<OverlayResult?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly MagnifierInput _magnifier = new((Keyboard.Modifiers & ModifierKeys.Control) != 0);
    private DragSelection? _drag;
    private SelectionOverlay? _dragOverlay;

    private bool _measuring;
    private PixelPoint _measurementStart;

    internal IReadOnlyList<SelectionOverlay> Overlays => _overlays;
    internal Task<OverlayResult?> Completion => _completion.Task;

    internal SelectionController(DesktopSnapshot snapshot)
    {
        _monitors = snapshot.Monitors.Select(monitor => monitor.Monitor).ToList();
        try
        {
            foreach (var monitor in snapshot.Monitors)
            {
                var overlay = new SelectionOverlay(monitor);
                overlay.MouseLeftButtonDown += OnMouseLeftButtonDown;
                overlay.MouseMove += OnMouseMove;
                overlay.MouseLeftButtonUp += OnMouseLeftButtonUp;
                overlay.LostMouseCapture += OnLostMouseCapture;
                overlay.MouseRightButtonUp += OnMouseRightButtonUp;
                overlay.KeyDown += OnKeyDown;
                overlay.KeyUp += OnKeyUp;
                overlay.LostKeyboardFocus += OnLostKeyboardFocus;
                overlay.DisplayChanged += OnDisplayChanged;
                _overlays.Add(overlay);
            }
        }
        catch
        {
            Close();
            throw;
        }
    }

    /// <summary>顯示覆蓋層並等待選取結果；取消時為 null，選取期間顯示設定變更時拋出 <see cref="CaptureException"/>。</summary>
    public static Task<OverlayResult?> SelectAsync(DesktopSnapshot snapshot)
    {
        var controller = new SelectionController(snapshot);
        controller.Show();
        return controller._completion.Task;
    }

    private void Show()
    {
        foreach (var overlay in _overlays)
        {
            overlay.ShowInactive();
        }

        // 游標所在顯示器的覆蓋層取得前景與鍵盤焦點，Esc 才能取消。處理程序為 Per-Monitor V2，游標位置為實體像素。
        var cursor = NativeMethods.GetCursorPos(out var position) ? new PixelPoint(position.X, position.Y) : default;
        (_overlays.FirstOrDefault(overlay => overlay.Monitor.Bounds.Contains(cursor)) ?? _overlays[0]).Activate();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var overlay = (SelectionOverlay)sender;
        BeginDrag(overlay, overlay.DesktopPosition(e));
        if (_drag is not null)
        {
            overlay.CaptureMouse();
        }
    }

    internal void BeginDrag(SelectionOverlay overlay, PixelPoint desktop)
    {
        _drag = DragSelection.Begin(_monitors, desktop);
        if (_drag is null)
        {
            return;
        }

        _dragOverlay = overlay;
        foreach (var candidate in _overlays)
        {
            candidate.ShowSelection(null);
            candidate.ShowMeasurement(null);
        }
        _measurementStart = CoordinateMapper.DesktopToMonitorLocal(desktop, overlay.Monitor.Bounds);
        if (_measuring)
        {
            UpdateDrag(desktop);
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        UpdateMagnifier(((SelectionOverlay)sender).DesktopPosition(e));
        UpdateDrag(((SelectionOverlay)sender).DesktopPosition(e));
    }

    internal void UpdateDrag(PixelPoint desktop)
    {
        if (_drag is { } drag && _dragOverlay is { } overlay)
        {
            if (_measuring)
            {
                var end = CoordinateMapper.DesktopToMonitorLocal(overlay.Monitor.Bounds.Clamp(desktop), overlay.Monitor.Bounds);
                overlay.ShowMeasurement(new PixelMeasurement(_measurementStart, end));
            }
            else
            {
                overlay.ShowSelection(drag.RectTo(desktop));
            }
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        EndDrag(((SelectionOverlay)sender).DesktopPosition(e));
    }

    internal void EndDrag(PixelPoint desktop)
    {
        if (_drag is not { } drag || _dragOverlay is not { } overlay)
        {
            return;
        }

        // 先結束拖曳再釋放捕捉，讓失去捕捉的處理知道這是正常結束。
        UpdateDrag(desktop);
        var result = drag.Complete(desktop);
        _drag = null;
        _dragOverlay = null;
        overlay.ReleaseMouseCapture();
        if (_measuring)
        {
            return;
        }

        if (result is not null)
        {
            Finish(new RegionSelected(result.Value));
            return;
        }

        // 在起點放開等於沒有拖曳：不產生結果，維持選取狀態讓使用者重新拖曳或取消。
        overlay.ShowSelection(null);
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        HideMagnifiers();
        AbortDrag();
    }

    internal void AbortDrag()
    {
        // 拖曳途中被其他視窗奪走滑鼠捕捉時放棄這次拖曳，覆蓋層維持在選取狀態。
        if (_dragOverlay is { } overlay)
        {
            _drag = null;
            _dragOverlay = null;
            overlay.ShowSelection(null);
            overlay.ShowMeasurement(null);
        }
    }

    // 在放開右鍵時才取消：若在按下時就關閉覆蓋層，放開的事件會落到底下的應用程式並開啟它的右鍵選單。
    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Finish(null);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Finish(null);
        }
        else if (e.Key == Key.R && !e.IsRepeat && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            ToggleMeasurement();
        }
        else if (e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            e.Handled = true;
            _magnifier.ControlDown(e.IsRepeat);
            if (TryGetCursor(out var cursor))
            {
                UpdateMagnifier(cursor);
            }
        }
        else if (e.Key == Key.C && !e.IsRepeat && Keyboard.Modifiers == ModifierKeys.None
            && TryGetCursor(out var cursor))
        {
            var overlay = _overlays.FirstOrDefault(candidate => candidate.Monitor.Bounds.Contains(cursor));
            if (overlay is not null)
            {
                e.Handled = true;
                Finish(new ColorPicked(overlay.ColorCodeAt(cursor)));
            }
        }
    }

    internal void ToggleMeasurement()
    {
        var captured = _dragOverlay;
        AbortDrag();
        captured?.ReleaseMouseCapture();
        _measuring = !_measuring;
        foreach (var overlay in _overlays)
        {
            overlay.ShowSelection(null);
            overlay.ShowMeasurement(null);
            overlay.SetMeasurementMode(_measuring);
        }
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            e.Handled = true;
            _magnifier.ControlUp(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl));
            HideMagnifiers();
        }
    }

    private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is not SelectionOverlay)
        {
            HideMagnifiers();
        }
    }

    private static bool TryGetCursor(out PixelPoint cursor)
    {
        var succeeded = NativeMethods.GetCursorPos(out var position);
        cursor = new PixelPoint(position.X, position.Y);
        return succeeded;
    }

    private void UpdateMagnifier(PixelPoint cursor)
    {
        foreach (var overlay in _overlays)
        {
            if (_magnifier.IsVisible && overlay.Monitor.Bounds.Contains(cursor))
            {
                overlay.ShowMagnifier(cursor);
            }
            else
            {
                overlay.HideMagnifier();
            }
        }
    }

    private void HideMagnifiers()
    {
        _magnifier.Hide();
        foreach (var overlay in _overlays)
        {
            overlay.HideMagnifier();
        }
    }

    private void OnDisplayChanged(object? sender, EventArgs e) =>
        Fail(new CaptureException(Strings.Get("Capture_DisplayChanged")));

    internal void Finish(OverlayResult? result)
    {
        Close();
        _completion.TrySetResult(result);
    }

    private void Fail(CaptureException error)
    {
        Close();
        _completion.TrySetException(error);
    }

    private void Close()
    {
        _drag = null;
        _dragOverlay = null;
        foreach (var overlay in _overlays)
        {
            overlay.Dispose();
        }

        _overlays.Clear();
    }
}
