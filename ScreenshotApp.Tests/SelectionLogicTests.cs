using ScreenshotApp.Capture;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;

namespace ScreenshotApp.Tests;

/// <summary>
/// 拖曳選取的幾何：起點決定顯示器、夾取在起始顯示器內、包含起點與終點像素、零尺寸不產生結果。
/// 顯示器配置：主螢幕、左側的非主螢幕（負 X），以及右上方解析度不同的顯示器（負 Y）。
/// </summary>
public class SelectionLogicTests
{
    private static readonly MonitorInfo Primary = new(0, new PixelRect(0, 0, 1920, 1080));
    private static readonly MonitorInfo Left = new(1, new PixelRect(-1920, 0, 1920, 1080));
    private static readonly MonitorInfo UpperRight = new(2, new PixelRect(1920, -300, 2560, 1440));
    private static readonly MonitorInfo[] Monitors = [Primary, Left, UpperRight];

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1919, 1079, 0)]
    [InlineData(-1, 0, 1)]
    [InlineData(-1920, 1079, 1)]
    [InlineData(1920, -300, 2)]
    [InlineData(4479, 1139, 2)]
    public void Begin_ChoosesTheMonitorContainingThePoint(int x, int y, int expectedIndex)
    {
        var drag = DragSelection.Begin(Monitors, new PixelPoint(x, y));

        Assert.NotNull(drag);
        Assert.Equal(expectedIndex, drag.Monitor.Index);
    }

    [Theory]
    [InlineData(-1921, 0)]
    [InlineData(100, 1080)]
    [InlineData(1920, 1140)]
    public void Begin_OutsideEveryMonitor_ReturnsNull(int x, int y)
    {
        Assert.Null(DragSelection.Begin(Monitors, new PixelPoint(x, y)));
    }

    [Theory]
    [InlineData(100, 100, 199, 149)]
    [InlineData(199, 149, 100, 100)]
    [InlineData(100, 149, 199, 100)]
    [InlineData(199, 100, 100, 149)]
    public void RectTo_AnyDragDirection_IncludesBothEndPixels(int startX, int startY, int endX, int endY)
    {
        var drag = DragSelection.Begin(Monitors, new PixelPoint(startX, startY))!;

        Assert.Equal(new PixelRect(100, 100, 100, 50), drag.RectTo(new PixelPoint(endX, endY)));
    }

    [Fact]
    public void RectTo_IsHalfOpen_EndPixelInsideAndNextPixelOutside()
    {
        var drag = DragSelection.Begin(Monitors, new PixelPoint(10, 20))!;

        var rect = drag.RectTo(new PixelPoint(29, 49))!.Value;

        Assert.Equal(30, rect.Right);
        Assert.Equal(50, rect.Bottom);
        Assert.True(rect.Contains(new PixelPoint(29, 49)));
        Assert.False(rect.Contains(new PixelPoint(30, 49)));
        Assert.False(rect.Contains(new PixelPoint(29, 50)));
    }

    [Fact]
    public void RectTo_StillOnTheStartPixel_ReturnsNull()
    {
        var drag = DragSelection.Begin(Monitors, new PixelPoint(500, 500))!;

        Assert.Null(drag.RectTo(new PixelPoint(500, 500)));
    }

    [Fact]
    public void RectTo_SingleRowOrColumn_IsOnePixelThick()
    {
        var drag = DragSelection.Begin(Monitors, new PixelPoint(500, 500))!;

        Assert.Equal(new PixelRect(500, 500, 1, 31), drag.RectTo(new PixelPoint(500, 530)));
        Assert.Equal(new PixelRect(470, 500, 31, 1), drag.RectTo(new PixelPoint(470, 500)));
    }

    [Fact]
    public void RectTo_CursorOnAnotherMonitor_IsClampedToTheStartMonitor()
    {
        var drag = DragSelection.Begin(Monitors, new PixelPoint(1900, 500))!;

        // 游標移到右上方的顯示器，選取只延伸到主螢幕的最後一行。
        Assert.Equal(new PixelRect(1900, 500, 20, 81), drag.RectTo(new PixelPoint(2500, 580)));
    }

    [Fact]
    public void RectTo_OnNegativeMonitor_ReturnsMonitorLocalCoordinates()
    {
        var drag = DragSelection.Begin(Monitors, new PixelPoint(-100, 10))!;

        // 往左上拖出顯示器邊界：夾取到 (-1920, 0)，換成左側顯示器的本地座標。
        Assert.Equal(new PixelRect(0, 0, 1821, 11), drag.RectTo(new PixelPoint(-3000, -50)));
    }

    [Fact]
    public void RectTo_OnMonitorAboveThePrimary_ReturnsMonitorLocalCoordinates()
    {
        var drag = DragSelection.Begin(Monitors, new PixelPoint(2000, -250))!;

        Assert.Equal(new PixelRect(80, 50, 21, 101), drag.RectTo(new PixelPoint(2020, -150)));
    }

    [Theory]
    [InlineData(1919, 1079)]
    [InlineData(5000, 5000)]
    public void RectTo_FromFirstToLastPixel_CoversTheWholeMonitor(int endX, int endY)
    {
        var drag = DragSelection.Begin(Monitors, new PixelPoint(0, 0))!;

        Assert.Equal(new PixelRect(0, 0, 1920, 1080), drag.RectTo(new PixelPoint(endX, endY)));
    }

    [Theory]
    [InlineData(-5000, -5000)]
    [InlineData(5000, -5000)]
    [InlineData(-5000, 5000)]
    [InlineData(5000, 5000)]
    [InlineData(2200, 0)]
    public void RectTo_AnyCursorPosition_StaysInsideTheStartMonitor(int endX, int endY)
    {
        var drag = DragSelection.Begin(Monitors, new PixelPoint(2000, -100))!;
        var localBounds = new PixelRect(0, 0, UpperRight.Bounds.Width, UpperRight.Bounds.Height);

        var rect = drag.RectTo(new PixelPoint(endX, endY));

        Assert.NotNull(rect);
        Assert.True(localBounds.Contains(rect.Value));
    }

    [Fact]
    public void Complete_AfterDragging_ReturnsStartMonitorAndRect()
    {
        var drag = DragSelection.Begin(Monitors, new PixelPoint(-1000, 300))!;

        var result = drag.Complete(new PixelPoint(-901, 399));

        Assert.Equal(new SelectionResult(1, new PixelRect(920, 300, 100, 100)), result);
    }

    [Fact]
    public void Complete_ReleasedOnTheStartPixel_ReturnsNull()
    {
        var drag = DragSelection.Begin(Monitors, new PixelPoint(300, 300))!;

        Assert.Null(drag.Complete(new PixelPoint(300, 300)));
    }

    [Fact]
    public void Complete_ClampedBackOntoTheStartPixel_ReturnsNull()
    {
        // 從右下角像素往顯示器外拖，夾取後仍是起點像素，等於沒有拖曳。
        var drag = DragSelection.Begin(Monitors, new PixelPoint(-1, 1079))!;

        Assert.Null(drag.Complete(new PixelPoint(-1, 1500)));
    }
}
