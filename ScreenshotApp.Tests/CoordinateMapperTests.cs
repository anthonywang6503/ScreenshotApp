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

    [Theory]
    [InlineData(1.0, 1.0, 1.0)]
    [InlineData(1.0, 1.75, 1.0 / 1.75)]
    [InlineData(2.0, 1.25, 1.6)]
    [InlineData(0.25, 2.0, 0.125)]
    [InlineData(4.0, 1.5, 4.0 / 1.5)]
    [InlineData(1.0 / 3, 1.0, 1.0 / 3)]
    public void ImageToViewScale_IsZoomDividedByDpiScale(double zoom, double dpiScale, double expected)
    {
        Assert.Equal(expected, CoordinateMapper.ImageToViewScale(zoom, dpiScale), 1e-12);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ImageToViewScale_InvalidZoom_ThrowsArgumentOutOfRange(double zoom)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CoordinateMapper.ImageToViewScale(zoom, 1.5));

        Assert.Equal("zoom", exception.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    public void ImageToViewScale_InvalidDpiScale_ThrowsArgumentOutOfRange(double dpiScale)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CoordinateMapper.ImageToViewScale(1, dpiScale));

        Assert.Equal("dpiScale", exception.ParamName);
    }

    [Fact]
    public void ImageToViewDipAndBack_ConcreteTransform_ScalesThenAddsOrigin()
    {
        var view = new ImageViewTransform(CoordinateMapper.ImageToViewScale(3, 1.5), new Point(10, -20));

        var dip = CoordinateMapper.ImageToViewDip(new Point(5, 7.5), view);

        Assert.Equal(new Point(20, -5), dip);
        Assert.Equal(new Point(5, 7.5), CoordinateMapper.ViewDipToImage(dip, view));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    [InlineData(3.0)]
    public void ViewDipToImage_AfterImageToViewDip_ReturnsOriginalPointWithinTinyError(double dpiScale)
    {
        double[] zooms = [0.25, 1.0 / 3, 0.5, 2.0 / 3, 1, 1.5, 2, 3, 4, 0.4537];
        Point[] origins = [new(0, 0), new(250, 120), new(-1000, -300), new(12.375, 7.6), new(-0.4, 33.33)];
        Point[] points = [new(0, 0), new(1, 1), new(1234.5, 987.25), new(3999, 0.5), new(-7.75, 4000)];

        foreach (var zoom in zooms)
        {
            var scale = CoordinateMapper.ImageToViewScale(zoom, dpiScale);
            foreach (var origin in origins)
            {
                var view = new ImageViewTransform(scale, origin);
                foreach (var point in points)
                {
                    var back = CoordinateMapper.ViewDipToImage(CoordinateMapper.ImageToViewDip(point, view), view);

                    var error = Math.Max(Math.Abs(back.X - point.X), Math.Abs(back.Y - point.Y));
                    Assert.True(error < 1e-9, $"縮放 {zoom}、圖片原點 {origin}、圖片座標 {point} 往返誤差為 {error}。");
                }
            }
        }
    }

    [Theory]
    [InlineData(1.25, 0)]
    [InlineData(1.25, 37)]
    [InlineData(1.25, 1201)]
    [InlineData(1.75, 0)]
    [InlineData(1.75, 37)]
    [InlineData(1.75, 1201)]
    [InlineData(1.0, 37)]
    [InlineData(1.5, 37)]
    [InlineData(2.0, 37)]
    public void ImageToPixel_CursorOnEveryPhysicalPixelAt100PercentZoom_MapsToThatImagePixelWithZeroError(double dpiScale, int originPhysical)
    {
        // 圖片原點位於實體像素 originPhysical；游標停在整數實體像素上，換成 DIP 後再換回圖片像素。
        var view = new ImageViewTransform(
            CoordinateMapper.ImageToViewScale(1, dpiScale), new Point(originPhysical / dpiScale, originPhysical / dpiScale));

        for (var pixel = 0; pixel <= 4000; pixel++)
        {
            var cursor = new Point((originPhysical + pixel) / dpiScale, (originPhysical + 4000 - pixel) / dpiScale);

            var image = CoordinateMapper.ImageToPixel(CoordinateMapper.ViewDipToImage(cursor, view));

            Assert.Equal(new PixelPoint(pixel, 4000 - pixel), image);
        }
    }

    [Theory]
    [InlineData(1.25, 0)]
    [InlineData(1.25, 37)]
    [InlineData(1.75, 0)]
    [InlineData(1.75, 37)]
    public void ImageToPixel_CursorAt400PercentZoom_FourConsecutivePhysicalPixelsMapToOneImagePixel(double dpiScale, int originPhysical)
    {
        var view = new ImageViewTransform(
            CoordinateMapper.ImageToViewScale(4, dpiScale), new Point(originPhysical / dpiScale, originPhysical / dpiScale));

        for (var physical = 0; physical < 4 * 1000; physical++)
        {
            var cursor = new Point((originPhysical + physical) / dpiScale, (originPhysical + physical) / dpiScale);

            var image = CoordinateMapper.ImageToPixel(CoordinateMapper.ViewDipToImage(cursor, view));

            // 實體像素 4k..4k+3 都顯示圖片像素 k。
            Assert.Equal(new PixelPoint(physical / 4, physical / 4), image);
        }
    }

    [Theory]
    [InlineData(3.25, 3)]
    [InlineData(3.999, 3)]
    [InlineData(5.0, 5)]
    [InlineData(4.9999999999, 5)] // 往返換算的浮點誤差不得讓游標落到前一個像素
    [InlineData(0.0, 0)]
    [InlineData(-0.5, -1)]
    [InlineData(-1.25, -2)]
    public void ImageToPixel_FloorsToContainingPixelButToleratesFloatingError(double coordinate, int expectedPixel)
    {
        Assert.Equal(new PixelPoint(expectedPixel, expectedPixel), CoordinateMapper.ImageToPixel(new Point(coordinate, coordinate)));
    }

    [Theory]
    [InlineData(1.0, 1.0, 8.0, 8.0)]
    [InlineData(1.0, 1.75, 8.0, 14.0)]
    [InlineData(2.0, 1.25, 8.0, 5.0)]
    [InlineData(0.5, 2.0, 8.0, 32.0)]
    [InlineData(4.0, 1.0, 6.0, 1.5)]
    public void ViewLengthToImage_DividesDipLengthByViewScale(double zoom, double dpiScale, double dipLength, double expected)
    {
        var view = new ImageViewTransform(CoordinateMapper.ImageToViewScale(zoom, dpiScale), new Point(-35.5, 120));

        Assert.Equal(expected, CoordinateMapper.ViewLengthToImage(dipLength, view), 1e-9);

        // 與同一段長度的兩端各自換算後的距離一致，不受圖片原點影響。
        var start = CoordinateMapper.ViewDipToImage(new Point(10, 20), view);
        var end = CoordinateMapper.ViewDipToImage(new Point(10 + dipLength, 20), view);
        Assert.Equal(expected, end.X - start.X, 1e-9);
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(1.0 / 3)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(4.0)]
    [InlineData(0.4537)]
    public void ScrollOffsetToKeep_AfterScrollingByOffset_ImagePointIsAtRequestedViewportPoint(double zoom)
    {
        var image = new Point(1234.5, 677.25);
        var contentOrigin = new Point(12, 8.5);
        var viewportPoint = new Point(400, 300);

        foreach (var dpiScale in new[] { 1.0, 1.25, 1.75 })
        {
            var scale = CoordinateMapper.ImageToViewScale(zoom, dpiScale);

            var offset = CoordinateMapper.ScrollOffsetToKeep(image, scale, contentOrigin, viewportPoint);

            // 捲動後，圖片原點在可視區域中的位置等於它在捲動內容中的位置減去捲動位移。
            var shownOrigin = new Point(contentOrigin.X - offset.X, contentOrigin.Y - offset.Y);
            var shown = CoordinateMapper.ImageToViewDip(image, new ImageViewTransform(scale, shownOrigin));
            Assert.Equal(viewportPoint.X, shown.X, 1e-9);
            Assert.Equal(viewportPoint.Y, shown.Y, 1e-9);
        }
    }

    [Fact]
    public void ScrollOffsetToKeep_ConcreteValues_IsContentPositionMinusViewportPoint()
    {
        var offset = CoordinateMapper.ScrollOffsetToKeep(new Point(300, 200), 2, new Point(10, 20), new Point(250, 150));

        Assert.Equal(new Point(360, 270), offset);
    }
}
