using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Tests;

public class PixelRectTests
{
    [Theory]
    [InlineData(-1, 10, "width")]
    [InlineData(10, -1, "height")]
    [InlineData(int.MinValue, 10, "width")]
    [InlineData(10, int.MinValue, "height")]
    public void Constructor_NegativeSize_ThrowsArgumentOutOfRange(int width, int height, string parameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new PixelRect(0, 0, width, height));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    public void RightAndBottom_AreExclusiveEdges()
    {
        var rect = new PixelRect(10, 20, 30, 40);

        Assert.Equal(40, rect.Right);
        Assert.Equal(60, rect.Bottom);
    }

    [Theory]
    [InlineData(10, 20)] // 左上角
    [InlineData(39, 59)] // Right - 1、Bottom - 1
    [InlineData(39, 20)]
    [InlineData(10, 59)]
    [InlineData(25, 40)]
    public void Contains_PointWithinHalfOpenBounds_ReturnsTrue(int x, int y)
    {
        Assert.True(new PixelRect(10, 20, 30, 40).Contains(new PixelPoint(x, y)));
    }

    [Theory]
    [InlineData(40, 20)] // 位於 Right
    [InlineData(10, 60)] // 位於 Bottom
    [InlineData(40, 60)]
    [InlineData(9, 20)] // X - 1
    [InlineData(10, 19)] // Y - 1
    public void Contains_PointOnExclusiveEdgeOrOutside_ReturnsFalse(int x, int y)
    {
        Assert.False(new PixelRect(10, 20, 30, 40).Contains(new PixelPoint(x, y)));
    }

    [Theory]
    [InlineData(10, 20, 0, 40)]
    [InlineData(10, 20, 30, 0)]
    [InlineData(0, 0, 0, 0)]
    public void Contains_EmptyRect_ReturnsFalseForPointsAroundIt(int x, int y, int width, int height)
    {
        var rect = new PixelRect(x, y, width, height);

        for (var dx = -2; dx <= 2; dx++)
        {
            for (var dy = -2; dy <= 2; dy++)
            {
                Assert.False(rect.Contains(new PixelPoint(x + dx, y + dy)));
            }
        }
    }

    [Theory]
    [InlineData(10, 20, 0, 40)]
    [InlineData(10, 20, 30, 0)]
    [InlineData(10, 20, 0, 0)]
    public void IsEmpty_ZeroWidthOrHeight_ReturnsTrue(int x, int y, int width, int height)
    {
        Assert.True(new PixelRect(x, y, width, height).IsEmpty);
    }

    [Fact]
    public void IsEmpty_PositiveSize_ReturnsFalse()
    {
        Assert.False(new PixelRect(10, 20, 1, 1).IsEmpty);
    }

    [Fact]
    public void Default_IsEmptyRectAtOrigin()
    {
        var rect = default(PixelRect);

        Assert.Equal(0, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal(0, rect.Width);
        Assert.Equal(0, rect.Height);
        Assert.True(rect.IsEmpty);
    }

    [Theory]
    [InlineData(10, 20, 110, 70)] // 往右下拖曳
    [InlineData(110, 70, 10, 20)] // 往左上拖曳
    [InlineData(10, 70, 110, 20)] // 往右上拖曳
    [InlineData(110, 20, 10, 70)] // 往左下拖曳
    public void FromCorners_AnyDragDirection_ReturnsSameNormalizedRect(int ax, int ay, int bx, int by)
    {
        Assert.Equal(new PixelRect(10, 20, 100, 50), PixelRect.FromCorners(new PixelPoint(ax, ay), new PixelPoint(bx, by)));
    }

    [Theory]
    [InlineData(10, 20, 10, 70)] // 同 X
    [InlineData(10, 20, 110, 20)] // 同 Y
    [InlineData(10, 20, 10, 20)] // 同一點
    public void FromCorners_SameXOrY_ReturnsEmptyRect(int ax, int ay, int bx, int by)
    {
        Assert.True(PixelRect.FromCorners(new PixelPoint(ax, ay), new PixelPoint(bx, by)).IsEmpty);
    }

    [Fact]
    public void Intersect_OverlappingRects_ReturnsOverlapRegardlessOfOrder()
    {
        var a = new PixelRect(0, 0, 100, 100);
        var b = new PixelRect(50, 30, 100, 100);

        Assert.Equal(new PixelRect(50, 30, 50, 70), a.Intersect(b));
        Assert.Equal(new PixelRect(50, 30, 50, 70), b.Intersect(a));
    }

    [Fact]
    public void Intersect_ContainedRect_ReturnsInnerRect()
    {
        var outer = new PixelRect(0, 0, 100, 100);
        var inner = new PixelRect(10, 20, 30, 40);

        Assert.Equal(inner, outer.Intersect(inner));
        Assert.Equal(inner, inner.Intersect(outer));
    }

    [Theory]
    [InlineData(100, 0, 50, 50)] // 右邊相接
    [InlineData(-50, 0, 50, 50)] // 左邊相接
    [InlineData(0, 100, 50, 50)] // 下邊相接
    [InlineData(0, -50, 50, 50)] // 上邊相接
    [InlineData(100, 100, 50, 50)] // 只有角點相接
    public void Intersect_RectsSharingOnlyAnEdge_ReturnsEmpty(int x, int y, int width, int height)
    {
        var rect = new PixelRect(0, 0, 100, 100);
        var other = new PixelRect(x, y, width, height);

        Assert.True(rect.Intersect(other).IsEmpty);
        Assert.True(other.Intersect(rect).IsEmpty);
    }

    [Theory]
    [InlineData(200, 300, 10, 10)]
    [InlineData(-500, 20, 10, 10)]
    [InlineData(20, 150, 10, 10)] // X 範圍重疊但 Y 分離
    public void Intersect_DisjointRects_ReturnsEmpty(int x, int y, int width, int height)
    {
        var rect = new PixelRect(0, 0, 100, 100);
        var other = new PixelRect(x, y, width, height);

        Assert.True(rect.Intersect(other).IsEmpty);
        Assert.True(other.Intersect(rect).IsEmpty);
    }

    [Theory]
    [InlineData(5, 7, 15, 27)]
    [InlineData(-15, -25, -5, -5)]
    [InlineData(-1930, -1100, -1920, -1080)]
    [InlineData(0, 0, 10, 20)]
    public void Offset_AnyDelta_MovesPositionAndKeepsSize(int dx, int dy, int expectedX, int expectedY)
    {
        Assert.Equal(new PixelRect(expectedX, expectedY, 30, 40), new PixelRect(10, 20, 30, 40).Offset(dx, dy));
    }

    [Theory]
    [InlineData(0, 30, 10, 30)] // 左側
    [InlineData(100, 30, 39, 30)] // 右側
    [InlineData(20, 0, 20, 20)] // 上方
    [InlineData(20, 100, 20, 59)] // 下方
    [InlineData(-5, -5, 10, 20)] // 左上外側
    [InlineData(25, 40, 25, 40)] // 內部不變
    [InlineData(40, 60, 39, 59)] // Right、Bottom 對應到最後一個像素
    public void Clamp_AnyPoint_ReturnsNearestPixelInsideRect(int x, int y, int expectedX, int expectedY)
    {
        Assert.Equal(new PixelPoint(expectedX, expectedY), new PixelRect(10, 20, 30, 40).Clamp(new PixelPoint(x, y)));
    }

    [Theory]
    [InlineData(10, 20, 0, 40)]
    [InlineData(10, 20, 30, 0)]
    [InlineData(0, 0, 0, 0)]
    public void Clamp_EmptyRect_ThrowsInvalidOperation(int x, int y, int width, int height)
    {
        var rect = new PixelRect(x, y, width, height);

        Assert.Throws<InvalidOperationException>(() => rect.Clamp(new PixelPoint(x, y)));
    }

    [Theory]
    [InlineData(10, 20, 100, 50)] // 完全相同
    [InlineData(30, 30, 10, 10)] // 內部
    [InlineData(10, 20, 1, 1)] // 左上角像素
    [InlineData(109, 69, 1, 1)] // 右下角像素
    public void Contains_RectWithinBounds_ReturnsTrue(int x, int y, int width, int height)
    {
        Assert.True(new PixelRect(10, 20, 100, 50).Contains(new PixelRect(x, y, width, height)));
    }

    [Theory]
    [InlineData(100, 30, 11, 10)] // 超出右邊
    [InlineData(30, 30, 10, 41)] // 超出下邊
    [InlineData(9, 30, 10, 10)] // 超出左邊
    [InlineData(30, 19, 10, 10)] // 超出上邊
    [InlineData(0, 0, 200, 200)] // 反過來包住此矩形
    [InlineData(200, 200, 10, 10)] // 完全分離
    public void Contains_RectCrossingOrOutsideBounds_ReturnsFalse(int x, int y, int width, int height)
    {
        Assert.False(new PixelRect(10, 20, 100, 50).Contains(new PixelRect(x, y, width, height)));
    }

    [Fact]
    public void RightAndBottom_MonitorAboveLeftOfPrimary_EndAtPrimaryOrigin()
    {
        var monitor = new PixelRect(-2560, -1440, 2560, 1440);

        Assert.Equal(0, monitor.Right);
        Assert.Equal(0, monitor.Bottom);
    }

    [Theory]
    [InlineData(-2560, -1440)]
    [InlineData(-1, -1)]
    [InlineData(-1280, -720)]
    public void Contains_PointOnNegativeCoordinateMonitor_ReturnsTrue(int x, int y)
    {
        Assert.True(new PixelRect(-2560, -1440, 2560, 1440).Contains(new PixelPoint(x, y)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(-2561, -1)]
    [InlineData(-1, -1441)]
    public void Contains_PointOutsideNegativeCoordinateMonitor_ReturnsFalse(int x, int y)
    {
        Assert.False(new PixelRect(-2560, -1440, 2560, 1440).Contains(new PixelPoint(x, y)));
    }

    [Fact]
    public void FromCorners_NegativeCoordinates_ReturnsNormalizedRect()
    {
        Assert.Equal(new PixelRect(-2560, -1440, 2560, 1440), PixelRect.FromCorners(new PixelPoint(0, 0), new PixelPoint(-2560, -1440)));
        Assert.Equal(new PixelRect(-100, -20, 130, 70), PixelRect.FromCorners(new PixelPoint(30, -20), new PixelPoint(-100, 50)));
    }

    [Theory]
    [InlineData(-5000, 500, -2560, -1)]
    [InlineData(100, -9999, -1, -1440)]
    [InlineData(-1280, -720, -1280, -720)]
    public void Clamp_NegativeCoordinateMonitor_ReturnsNearestPixel(int x, int y, int expectedX, int expectedY)
    {
        var monitor = new PixelRect(-2560, -1440, 2560, 1440);

        Assert.Equal(new PixelPoint(expectedX, expectedY), monitor.Clamp(new PixelPoint(x, y)));
    }

    [Fact]
    public void Intersect_SelectionAcrossMonitorLeftOfPrimary_SplitsAtPrimaryOrigin()
    {
        var left = new PixelRect(-1920, 0, 1920, 1080);
        var primary = new PixelRect(0, 0, 1920, 1080);
        var selection = new PixelRect(-100, 10, 300, 20);

        Assert.Equal(new PixelRect(-100, 10, 100, 20), selection.Intersect(left));
        Assert.Equal(new PixelRect(0, 10, 200, 20), selection.Intersect(primary));
        Assert.True(left.Intersect(primary).IsEmpty);
    }
}
