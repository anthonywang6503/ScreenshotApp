using ScreenshotApp.Capture;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Tests;

public class MagnifierGeometryTests
{
    [Theory]
    [InlineData(0, 0, -7, -7, 24, 24)]
    [InlineData(799, 0, 792, -7, 595, 24)]
    [InlineData(0, 599, -7, 592, 24, 355)]
    [InlineData(799, 599, 792, 592, 595, 355)]
    public void AT11_Corners_KeepCursorAtSampleCenterAndFlipPanel(int x, int y, int sx, int sy, int px, int py)
    {
        var layout = MagnifierGeometry.Calculate(800, 600, new PixelPoint(x, y), 15, 12, 160, 40);

        Assert.Equal(new PixelRect(sx, sy, 15, 15), layout.Sample);
        Assert.Equal(new PixelRect(px, py, 180, 220), layout.Panel);
        Assert.Equal(x, layout.Sample.X + layout.Sample.Width / 2);
        Assert.Equal(y, layout.Sample.Y + layout.Sample.Height / 2);
    }

    [Theory]
    [InlineData(596, 376, 620, 400)]
    [InlineData(597, 377, 393, 153)]
    public void AT11_EdgeFlip_IncludesLabelAndGap(int x, int y, int expectedX, int expectedY)
    {
        var layout = MagnifierGeometry.Calculate(800, 600, new PixelPoint(x, y), 15, 12, 100, 20);
        Assert.Equal(new PixelRect(expectedX, expectedY, 180, 200), layout.Panel);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.75)]
    [InlineData(2)]
    public void AT11_EveryCellBoundary_MapsToAnIntegerPhysicalPixel(double dpi)
    {
        var layout = MagnifierGeometry.Calculate(1920, 1080, new PixelPoint(1900, 1000), 15, 12, 220, 50);
        for (var cell = 0; cell <= 15; cell++)
        {
            var pixel = new PixelPoint(layout.Panel.X + cell * 12, layout.Panel.Y + cell * 12);
            var dip = CoordinateMapper.MonitorLocalToViewDip(pixel, dpi);
            Assert.Equal(pixel, CoordinateMapper.ViewDipToMonitorLocal(dip, dpi));
            Assert.Equal(pixel.X, dip.X * dpi, 8);
            Assert.Equal(pixel.Y, dip.Y * dpi, 8);
        }
    }

    [Fact]
    public void Panel_WideLabelAndInsufficientOppositeSpace_ClampToMonitor()
    {
        var layout = MagnifierGeometry.Calculate(320, 300, new PixelPoint(150, 120), 15, 12, 250, 40);
        Assert.Equal(new PixelRect(0, 0, 250, 220), layout.Panel);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(14, 12)]
    [InlineData(15, 0)]
    public void InvalidGrid_Throws(int count, int cell)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MagnifierGeometry.Calculate(800, 600, default, count, cell, 100, 20));
    }

    [Fact]
    public void ControlHeldOnEntry_RequiresReleaseAndFreshPress()
    {
        var input = new MagnifierInput(controlInitiallyDown: true);
        input.ControlDown(isRepeat: false);
        Assert.False(input.IsVisible);
        input.ControlDown(isRepeat: true);
        Assert.False(input.IsVisible);
        input.ControlUp(controlStillDown: false);
        input.ControlDown(isRepeat: true);
        Assert.False(input.IsVisible);
        input.ControlDown(isRepeat: false);
        Assert.True(input.IsVisible);
        input.ControlUp(controlStillDown: false);
        Assert.False(input.IsVisible);
    }

    [Fact]
    public void BothControlKeysHeldOnEntry_RequiresBothToBeReleased()
    {
        var input = new MagnifierInput(controlInitiallyDown: true);
        input.ControlUp(controlStillDown: true);
        input.ControlDown(isRepeat: false);
        Assert.False(input.IsVisible);
        input.ControlUp(controlStillDown: false);
        input.ControlDown(isRepeat: false);
        Assert.True(input.IsVisible);
    }

    [Fact]
    public void CaptureLost_HidesAndRepeatDoesNotReopen()
    {
        var input = new MagnifierInput(controlInitiallyDown: false);
        input.ControlDown(isRepeat: false);
        Assert.True(input.IsVisible);
        input.Hide();
        input.ControlDown(isRepeat: true);
        Assert.False(input.IsVisible);
        input.ControlUp(controlStillDown: false);
        input.ControlDown(isRepeat: false);
        Assert.True(input.IsVisible);
    }
}
