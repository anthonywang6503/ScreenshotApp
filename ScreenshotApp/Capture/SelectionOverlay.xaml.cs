using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;
using ScreenshotApp.Resources;

namespace ScreenshotApp.Capture;

/// <summary>
/// 單一顯示器的區域選取覆蓋層。HWND 直接以該顯示器的實體像素邊界建立，一建立就是該顯示器的 DPI，
/// 不經過跨 DPI 的移動；覆蓋層不是 WPF Window，生命週期由選取控制器管理。
/// </summary>
internal sealed partial class SelectionOverlay : UserControl, IDisposable
{
    private const double LabelGap = 4;

    private readonly HwndSource _source;
    private readonly Rect _area;
    private readonly RectangleGeometry _hole = new(Rect.Empty);
    private readonly RectangleGeometry _frameOuter = new(Rect.Empty);
    private readonly RectangleGeometry _frameInner = new(Rect.Empty);

    public SelectionOverlay(MonitorSnapshot snapshot)
    {
        Monitor = snapshot.Monitor;
        InitializeComponent();
        SnapshotImage.Source = snapshot.Image;

        var bounds = Monitor.Bounds;
        _source = new HwndSource(new HwndSourceParameters(Strings.Get("Selection_Title"))
        {
            PositionX = bounds.X,
            PositionY = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            WindowStyle = NativeMethods.WS_POPUP,
            // 置頂且不出現在工作列與 Alt+Tab。
            ExtendedWindowStyle = NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_TOOLWINDOW,
            HwndSourceHook = WndProc,
        });
        _source.RootVisual = this;

        _area = CoordinateMapper.MonitorLocalToViewDip(new PixelRect(0, 0, bounds.Width, bounds.Height), DpiScale);
        Mask.Data = new GeometryGroup
        {
            FillRule = FillRule.EvenOdd,
            Children = { new RectangleGeometry(_area), _hole },
        };
        SelectionFrame.Data = new GeometryGroup
        {
            FillRule = FillRule.EvenOdd,
            Children = { _frameOuter, _frameInner },
        };
    }

    /// <summary>選取期間顯示設定變更（解析度、排列、縮放比例或插拔顯示器），快照已與畫面不一致。</summary>
    public event EventHandler? DisplayChanged;

    public MonitorInfo Monitor { get; }

    private double DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    // 選取框寬度（實體像素）：約一個 DIP，至少 1 像素。
    private int FrameWidth => Math.Max(1, (int)Math.Round(DpiScale));

    /// <summary>
    /// 滑鼠事件發生時的桌面實體像素位置。取自事件本身而非處理當下的游標位置：UI 執行緒忙碌而延後處理時，
    /// 游標可能已經移開。WPF 以覆蓋層的 DPI 把訊息中的實體像素換成 DIP，這裡以同一比例換回，整數像素往返沒有誤差。
    /// </summary>
    public PixelPoint DesktopPosition(MouseEventArgs e) =>
        CoordinateMapper.MonitorLocalToDesktop(CoordinateMapper.ViewDipToMonitorLocal(e.GetPosition(this), DpiScale), Monitor.Bounds);

    /// <summary>顯示但不搶前景。</summary>
    public void ShowInactive() => NativeMethods.ShowWindow(_source.Handle, NativeMethods.SW_SHOWNA);

    /// <summary>帶到前景並取得鍵盤焦點，讓 Esc 可以取消。</summary>
    public void Activate()
    {
        NativeMethods.SetForegroundWindow(_source.Handle);
        Keyboard.Focus(this);
    }

    /// <summary>顯示選取範圍（顯示器本地實體像素）；null 表示沒有選取，整個畫面維持暗化。</summary>
    public void ShowSelection(PixelRect? selection)
    {
        if (selection is not { } rect)
        {
            _hole.Rect = Rect.Empty;
            SelectionFrame.Visibility = Visibility.Collapsed;
            SizeLabel.Visibility = Visibility.Collapsed;
            return;
        }

        var view = CoordinateMapper.MonitorLocalToViewDip(rect, DpiScale);
        _hole.Rect = view;

        // 外框是選取範圍外側一圈整數像素寬的環，以實體像素計算邊界，不會蓋住選到的像素。
        var frame = CoordinateMapper.MonitorLocalToViewDip(
            new PixelRect(rect.X - FrameWidth, rect.Y - FrameWidth, rect.Width + 2 * FrameWidth, rect.Height + 2 * FrameWidth),
            DpiScale);
        _frameOuter.Rect = frame;
        _frameInner.Rect = view;
        SelectionFrame.Visibility = Visibility.Visible;

        SizeText.Text = Strings.Format("Selection_Size", rect.Width, rect.Height);
        SizeLabel.Visibility = Visibility.Visible;
        SizeLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var label = SizeLabel.DesiredSize;

        // 標籤放在選取框左上方；上方空間不足時改放在範圍內側。
        var top = frame.Y - LabelGap - label.Height;
        Canvas.SetTop(SizeLabel, top >= _area.Top ? top : view.Y + LabelGap);
        Canvas.SetLeft(SizeLabel, Math.Max(_area.Left, Math.Min(view.X, _area.Right - label.Width)));
    }

    public void Dispose() => _source.Dispose();

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg is NativeMethods.WM_DISPLAYCHANGE or NativeMethods.WM_DPICHANGED)
        {
            // 覆蓋層建立後從不移動，收到 WM_DPICHANGED 只可能是縮放比例被變更；不讓 WPF 依建議矩形改變覆蓋層大小。
            handled = msg == NativeMethods.WM_DPICHANGED;

            // 延後通知，避免在這個視窗自己的訊息處理途中關閉它。
            Dispatcher.BeginInvoke(new Action(() => DisplayChanged?.Invoke(this, EventArgs.Empty)));
        }

        return 0;
    }
}
