using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ScreenshotApp.Capture;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

[Collection(GdiObjectCountCollection.Name)]
public class MeasurementOverlayTests
{
    [Fact]
    public void AT14_MeasurementReleasePersists_NextMonitorReplaces_AndNeverCompletesCapture() => StaRunner.Run(() =>
    {
        var controller = Create();
        try
        {
            controller.ToggleMeasurement();
            var first = controller.Overlays[0];
            controller.BeginDrag(first, new(-390, 20));
            controller.UpdateDrag(new(-387, 24));
            controller.EndDrag(new(-387, 24));
            Assert.Equal("長度 5 px　Δx 3　Δy 4　4 × 5 px", Text(first));
            Assert.Equal(Visibility.Visible, Line(first).Visibility);
            Assert.False(controller.Completion.IsCompleted);
            var second = controller.Overlays[1];
            controller.BeginDrag(second, new(30, 40));
            Assert.Equal(Visibility.Collapsed, Line(first).Visibility);
            controller.EndDrag(new(30, 40));
            Assert.Equal("長度 0 px　Δx 0　Δy 0　1 × 1 px", Text(second));
            Assert.Equal(Visibility.Visible, Line(second).Visibility);
            second.ShowMagnifier(new(30, 40));
            Assert.Equal(Visibility.Visible, ((Canvas)second.FindName("MagnifierPanel")).Visibility);
            Assert.Equal(Visibility.Visible, Line(second).Visibility);
            Assert.False(controller.Completion.IsCompleted);
        }
        finally { controller.Finish(null); }
        Assert.Null(controller.Completion.Result);
    });

    [Fact]
    public void AT14_DragIsClampedToStartingMonitor_AndLostCaptureClearsOnlyPendingMeasurement() => StaRunner.Run(() =>
    {
        var controller = Create();
        try
        {
            controller.ToggleMeasurement();
            var overlay = controller.Overlays[0];
            controller.BeginDrag(overlay, new(-2, 100));
            controller.UpdateDrag(new(50, 100));
            Assert.Equal("長度 1 px　Δx 1　Δy 0　2 × 1 px", Text(overlay));
            controller.AbortDrag();
            Assert.Equal(Visibility.Collapsed, Line(overlay).Visibility);
            controller.EndDrag(new(50, 100));
            Assert.False(controller.Completion.IsCompleted);
            controller.BeginDrag(overlay, new(-2, 100));
            controller.EndDrag(new(-1, 100));
            controller.AbortDrag();
            Assert.Equal(Visibility.Visible, Line(overlay).Visibility);
        }
        finally { controller.Finish(null); }
    });

    [Fact]
    public void AT14_ModeSwitchDiscardsPendingSelectionAndMeasurement_ThenNormalCaptureStillWorks() => StaRunner.Run(() =>
    {
        var controller = Create();
        try
        {
            var overlay = controller.Overlays[1];
            controller.BeginDrag(overlay, new(10, 20));
            controller.UpdateDrag(new(30, 40));
            controller.ToggleMeasurement();
            controller.EndDrag(new(30, 40));
            Assert.False(controller.Completion.IsCompleted);
            Assert.Equal(Visibility.Collapsed, ((Border)overlay.FindName("SizeLabel")).Visibility);
            Assert.Contains("量測模式", ((TextBlock)overlay.FindName("HelpText")).Text);
            controller.BeginDrag(overlay, new(10, 20));
            controller.UpdateDrag(new(30, 40));
            controller.ToggleMeasurement();
            Assert.Equal(Visibility.Collapsed, Line(overlay).Visibility);
            controller.EndDrag(new(30, 40));
            Assert.False(controller.Completion.IsCompleted);
            controller.BeginDrag(overlay, new(10, 20));
            controller.EndDrag(new(13, 24));
            Assert.True(controller.Completion.IsCompletedSuccessfully);
            Assert.Equal(new PixelRect(10, 20, 4, 5), Assert.IsType<RegionSelected>(controller.Completion.Result).Selection.Rect);
        }
        finally { controller.Finish(null); }
    });

    [Fact]
    public void AT14_HiddenOverlay_UsesPixelCenterCoordinatesAndKeepsLabelOnScreen() => StaRunner.Run(() =>
    {
        var controller = Create();
        try
        {
            var overlay = controller.Overlays[1];
            var value = new PixelMeasurement(new(396, 295), new(399, 299));
            overlay.ShowMeasurement(value);
            var scale = VisualTreeHelper.GetDpi(overlay).DpiScaleX;
            var line = Line(overlay);
            Assert.Equal(396.5, line.X1 * scale, 8);
            Assert.Equal(299.5, line.Y2 * scale, 8);
            var label = (Border)overlay.FindName("MeasurementLabel");
            Assert.InRange(Canvas.GetLeft(label) + label.DesiredSize.Width, 0, 400 / scale + 0.001);
            Assert.InRange(Canvas.GetTop(label) + label.DesiredSize.Height, 0, 300 / scale + 0.001);
            Assert.Contains(value.DisplayLength + " px", Text(overlay));
        }
        finally { controller.Finish(null); }
    });

    private static Line Line(SelectionOverlay overlay) => (Line)overlay.FindName("MeasurementLine");
    private static string Text(SelectionOverlay overlay) => ((TextBlock)overlay.FindName("MeasurementText")).Text;
    private static SelectionController Create()
    {
        var image = BitmapSource.Create(400, 300, 96, 96, PixelFormats.Bgr32, null, new byte[400 * 300 * 4], 1600);
        image.Freeze();
        return new SelectionController(new DesktopSnapshot([
            new(new MonitorInfo(0, new(-400, 0, 400, 300)), image),
            new(new MonitorInfo(1, new(0, 0, 400, 300)), image),
        ]));
    }
}
