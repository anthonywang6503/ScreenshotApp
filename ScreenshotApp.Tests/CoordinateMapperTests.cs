using System.Windows;
using ScreenshotApp.Capture;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Tests;

public class CoordinateMapperTests
{
    [Theory]
    [InlineData(0, 0, 1920, 1080, 100, 200, 100, 200)]
    [InlineData(0, 0, 1920, 1080, 1919, 1079, 1919, 1079)]
    [InlineData(-1920, 0, 1920, 1080, -1920, 0, 0, 0)]
    [InlineData(-1920, 0, 1920, 1080, -1, 1079, 1919, 1079)]
    [InlineData(-1920, 0, 1920, 1080, 0, 0, 1920, 0)] // 顯示器外的點不受限制
    [InlineData(3456, -500, 2560, 1440, 3456, -500, 0, 0)]
    [InlineData(3456, -500, 2560, 1440, 4000, 0, 544, 500)]
    [InlineData(3456, -500, 2560, 1440, 6015, 939, 2559, 1439)]
    [InlineData(-2560, -1440, 2560, 1440, -2560, -1440, 0, 0)]
    [InlineData(-2560, -1440, 2560, 1440, -1000, -300, 1560, 1140)]
    [InlineData(-2560, -1440, 2560, 1440, -1, -1, 2559, 1439)]
    public void DesktopToMonitorLocal_Point_SubtractsMonitorOriginAndRoundTrips(
        int monitorX, int monitorY, int monitorWidth, int monitorHeight,
        int desktopX, int desktopY, int expectedX, int expectedY)
    {
        var monitor = new PixelRect(monitorX, monitorY, monitorWidth, monitorHeight);
        var desktop = new PixelPoint(desktopX, desktopY);

        var local = CoordinateMapper.DesktopToMonitorLocal(desktop, monitor);

        Assert.Equal(new PixelPoint(expectedX, expectedY), local);
        Assert.Equal(desktop, CoordinateMapper.MonitorLocalToDesktop(local, monitor));
    }

    [Theory]
    [InlineData(0, 0, 1920, 1080, 100, 200, 100, 200)]
    [InlineData(-1920, 0, 1920, 1080, -1820, 200, 100, 200)]
    [InlineData(3456, -500, 2560, 1440, 5000, -100, 1544, 400)]
    [InlineData(-2560, -1440, 2560, 1440, -2000, -1000, 560, 440)]
    public void DesktopToMonitorLocal_Rect_SubtractsMonitorOriginKeepsSizeAndRoundTrips(
        int monitorX, int monitorY, int monitorWidth, int monitorHeight,
        int desktopX, int desktopY, int expectedX, int expectedY)
    {
        var monitor = new PixelRect(monitorX, monitorY, monitorWidth, monitorHeight);
        var selection = new PixelRect(desktopX, desktopY, 300, 400);

        var local = CoordinateMapper.DesktopToMonitorLocal(selection, monitor);

        Assert.Equal(new PixelRect(expectedX, expectedY, 300, 400), local);
        Assert.Equal(selection, CoordinateMapper.MonitorLocalToDesktop(local, monitor));
    }

    [Theory]
    [InlineData(0, 0, 1920, 1080)]
    [InlineData(-1920, 0, 1920, 1080)]
    [InlineData(3456, -500, 2560, 1440)]
    [InlineData(-2560, -1440, 2560, 1440)]
    public void DesktopToMonitorLocal_MonitorBounds_StartsAtLocalOriginAndRoundTrips(
        int monitorX, int monitorY, int monitorWidth, int monitorHeight)
    {
        var monitor = new PixelRect(monitorX, monitorY, monitorWidth, monitorHeight);

        var local = CoordinateMapper.DesktopToMonitorLocal(monitor, monitor);

        Assert.Equal(new PixelRect(0, 0, monitorWidth, monitorHeight), local);
        Assert.Equal(monitor, CoordinateMapper.MonitorLocalToDesktop(local, monitor));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    [InlineData(2.25)]
    [InlineData(2.5)]
    [InlineData(3.0)]
    public void ViewDipToMonitorLocal_EveryPixelFrom0To4000_RoundTripsWithZeroError(double dpiScale)
    {
        for (var pixel = 0; pixel <= 4000; pixel++)
        {
            var local = new PixelPoint(pixel, 4000 - pixel);

            var dip = CoordinateMapper.MonitorLocalToViewDip(local, dpiScale);

            Assert.Equal(local, CoordinateMapper.ViewDipToMonitorLocal(dip, dpiScale));
        }
    }

    [Theory]
    [InlineData(3456, 1.75, 1974.857142857143)]
    [InlineData(100, 1.75, 57.142857142857146)]
    [InlineData(1920, 1.0, 1920.0)]
    [InlineData(1920, 1.25, 1536.0)]
    [InlineData(2560, 1.5, 1706.6666666666667)]
    [InlineData(1000, 3.0, 333.3333333333333)]
    public void MonitorLocalToViewDip_Point_DividesByDpiScale(int pixel, double dpiScale, double expectedDip)
    {
        var dip = CoordinateMapper.MonitorLocalToViewDip(new PixelPoint(pixel, pixel), dpiScale);

        Assert.Equal(expectedDip, dip.X, 1e-9);
        Assert.Equal(expectedDip, dip.Y, 1e-9);
    }

    [Theory]
    [InlineData(1920, 1080, 1.0)]
    [InlineData(1920, 1080, 1.25)]
    [InlineData(2560, 1440, 1.5)]
    [InlineData(3456, 2160, 1.75)]
    [InlineData(3840, 2160, 2.0)]
    [InlineData(2880, 1920, 2.25)]
    [InlineData(3840, 2400, 2.5)]
    [InlineData(5760, 3240, 3.0)]
    public void MonitorLocalToViewDip_WholeMonitorRect_SizeIsPixelsDividedByDpiScale(int width, int height, double dpiScale)
    {
        var dip = CoordinateMapper.MonitorLocalToViewDip(new PixelRect(0, 0, width, height), dpiScale);

        Assert.Equal(0.0, dip.X);
        Assert.Equal(0.0, dip.Y);
        Assert.Equal(width / dpiScale, dip.Width, 1e-9);
        Assert.Equal(height / dpiScale, dip.Height, 1e-9);
    }

    [Fact]
    public void MonitorLocalToViewDip_RectAt125Percent_ReturnsConcreteDipRect()
    {
        var dip = CoordinateMapper.MonitorLocalToViewDip(new PixelRect(100, 200, 300, 400), 1.25);

        Assert.Equal(new Rect(80, 160, 240, 320), dip);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    [InlineData(2.25)]
    [InlineData(2.5)]
    [InlineData(3.0)]
    public void MonitorLocalToViewDip_Rect_EachEdgeMapsBackToSamePixelGridLine(double dpiScale)
    {
        var local = new PixelRect(123, 457, 1001, 333);

        var dip = CoordinateMapper.MonitorLocalToViewDip(local, dpiScale);

        Assert.Equal(local.X / dpiScale, dip.Left, 1e-9);
        Assert.Equal(local.Y / dpiScale, dip.Top, 1e-9);
        Assert.Equal(local.Right / dpiScale, dip.Right, 1e-9);
        Assert.Equal(local.Bottom / dpiScale, dip.Bottom, 1e-9);
        Assert.Equal(new PixelPoint(local.X, local.Y), CoordinateMapper.ViewDipToMonitorLocal(dip.TopLeft, dpiScale));
        Assert.Equal(new PixelPoint(local.Right, local.Bottom), CoordinateMapper.ViewDipToMonitorLocal(dip.BottomRight, dpiScale));
    }

    [Theory]
    [InlineData(0.5, 1.0, 1)]
    [InlineData(2.5, 1.0, 3)]
    [InlineData(-0.5, 1.0, -1)]
    [InlineData(-2.5, 1.0, -3)]
    [InlineData(1.25, 2.0, 3)] // 2.5 像素
    [InlineData(0.2, 1.75, 0)] // 0.35 像素
    [InlineData(0.3, 1.75, 1)] // 0.525 像素
    public void ViewDipToMonitorLocal_FractionalPixel_RoundsToNearestWithMidpointAwayFromZero(
        double dip, double dpiScale, int expectedPixel)
    {
        Assert.Equal(new PixelPoint(expectedPixel, expectedPixel), CoordinateMapper.ViewDipToMonitorLocal(new Point(dip, dip), dpiScale));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void DpiScaleConversions_InvalidDpiScale_ThrowArgumentOutOfRange(double dpiScale)
    {
        var toDipPoint = Assert.Throws<ArgumentOutOfRangeException>(
            () => CoordinateMapper.MonitorLocalToViewDip(new PixelPoint(10, 10), dpiScale));
        var toDipRect = Assert.Throws<ArgumentOutOfRangeException>(
            () => CoordinateMapper.MonitorLocalToViewDip(new PixelRect(0, 0, 10, 10), dpiScale));
        var toPixel = Assert.Throws<ArgumentOutOfRangeException>(
            () => CoordinateMapper.ViewDipToMonitorLocal(new Point(10, 10), dpiScale));

        Assert.Equal("dpiScale", toDipPoint.ParamName);
        Assert.Equal("dpiScale", toDipRect.ParamName);
        Assert.Equal("dpiScale", toPixel.ParamName);
    }

    [Fact]
    public void DesktopToViewDipAndBack_PointOnNegativeMonitorAt150Percent_PassesThroughExpectedValues()
    {
        var monitor = new PixelRect(-2560, -1440, 2560, 1440);
        var desktop = new PixelPoint(-1000, -300);

        var local = CoordinateMapper.DesktopToMonitorLocal(desktop, monitor);
        var dip = CoordinateMapper.MonitorLocalToViewDip(local, 1.5);
        var backToLocal = CoordinateMapper.ViewDipToMonitorLocal(dip, 1.5);

        Assert.Equal(new PixelPoint(1560, 1140), local);
        Assert.Equal(new Point(1040, 760), dip);
        Assert.Equal(local, backToLocal);
        Assert.Equal(desktop, CoordinateMapper.MonitorLocalToDesktop(backToLocal, monitor));
    }

    [Theory]
    [InlineData(-2560, -1440)] // 顯示器左上角
    [InlineData(-1, -1)] // 顯示器右下角像素
    [InlineData(-2559, -1)]
    [InlineData(-7, -1439)]
    [InlineData(-1281, -721)]
    public void DesktopToViewDipAndBack_PointOnNegativeMonitorAt150Percent_ReturnsOriginalPoint(int desktopX, int desktopY)
    {
        var monitor = new PixelRect(-2560, -1440, 2560, 1440);
        var desktop = new PixelPoint(desktopX, desktopY);

        var local = CoordinateMapper.DesktopToMonitorLocal(desktop, monitor);
        var dip = CoordinateMapper.MonitorLocalToViewDip(local, 1.5);
        var backToLocal = CoordinateMapper.ViewDipToMonitorLocal(dip, 1.5);

        Assert.Equal(desktop, CoordinateMapper.MonitorLocalToDesktop(backToLocal, monitor));
    }
}
