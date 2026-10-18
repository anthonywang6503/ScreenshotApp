using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Tests;

/// <summary>
/// 驗證標註以整數圖片像素描述的幾何：範圍、控制點、平移、以控制點調整大小（含拖過對邊翻轉與最小 1×1），
/// 命中判定（筆劃帶、空心內側、通過像素中心的線段、箭頭頭部、圓盤半徑），以及重疊時由上而下的選取順序。
/// </summary>
public class AnnotationGeometryTests
{
    private static readonly Color Red = Colors.Red;

    [Fact]
    public void Bounds_BoxAnnotation_EqualsPixelRect()
    {
        Assert.Equal(new Rect(10, 20, 30, 40), Rectangle(new PixelRect(10, 20, 30, 40), strokeWidth: 3).Bounds);
        Assert.Equal(new Rect(10, 20, 30, 40), Ellipse(new PixelRect(10, 20, 30, 40), strokeWidth: 3).Bounds);
    }

    [Fact]
    public void Handles_BoxAnnotation_AreCornersAndEdgeMidpointsOnGridLines()
    {
        var expected = new AnnotationHandle[]
        {
            new(HandleKind.TopLeft, new Point(10, 20)),
            new(HandleKind.Top, new Point(25, 20)),
            new(HandleKind.TopRight, new Point(40, 20)),
            new(HandleKind.Right, new Point(40, 40)),
            new(HandleKind.BottomRight, new Point(40, 60)),
            new(HandleKind.Bottom, new Point(25, 60)),
            new(HandleKind.BottomLeft, new Point(10, 60)),
            new(HandleKind.Left, new Point(10, 40)),
        };

        Assert.Equal(expected, Rectangle(new PixelRect(10, 20, 30, 40), strokeWidth: 3).Handles);
        Assert.Equal(expected, Ellipse(new PixelRect(10, 20, 30, 40), strokeWidth: 3).Handles);
    }

    [Fact]
    public void Translate_BoxAnnotation_OffsetsRectAndKeepsEverythingElse()
    {
        var rectangle = Rectangle(new PixelRect(10, 20, 30, 40), strokeWidth: 3);
        var ellipse = Ellipse(new PixelRect(10, 20, 30, 40), strokeWidth: 3);

        Assert.Equal(rectangle with { Rect = new PixelRect(15, 13, 30, 40) }, rectangle.Translate(5, -7));
        Assert.Equal(ellipse with { Rect = new PixelRect(15, 13, 30, 40) }, ellipse.Translate(5, -7));
    }

    // 範圍 (10,20,30,40)：最外圍的像素為左 10、右 39、上 20、下 59。邊上的控制點只移動一個方向，另一軸的位移被忽略。
    [Theory]
    [InlineData(nameof(HandleKind.TopLeft), -5, -6, 5, 14, 35, 46)]
    [InlineData(nameof(HandleKind.Top), -5, -6, 10, 14, 30, 46)]
    [InlineData(nameof(HandleKind.TopRight), 5, -6, 10, 14, 35, 46)]
    [InlineData(nameof(HandleKind.Right), 5, -6, 10, 20, 35, 40)]
    [InlineData(nameof(HandleKind.BottomRight), 5, 6, 10, 20, 35, 46)]
    [InlineData(nameof(HandleKind.Bottom), 5, 6, 10, 20, 30, 46)]
    [InlineData(nameof(HandleKind.BottomLeft), -5, 6, 5, 20, 35, 46)]
    [InlineData(nameof(HandleKind.Left), -5, 6, 5, 20, 35, 40)]
    [InlineData(nameof(HandleKind.Start), 5, 6, 10, 20, 30, 40)] // 線段端點不是矩形範圍的控制點：不變
    [InlineData(nameof(HandleKind.End), 5, 6, 10, 20, 30, 40)]
    public void Resize_BoxAnnotationHandle_MovesOnlyTheMatchingEdges(string handle, int dx, int dy, int x, int y, int width, int height)
    {
        var original = Rectangle(new PixelRect(10, 20, 30, 40), strokeWidth: 3);

        var resized = original.Resize(Enum.Parse<HandleKind>(handle), dx, dy);

        Assert.Equal(original with { Rect = new PixelRect(x, y, width, height) }, resized);
    }

    [Theory]
    [InlineData(nameof(HandleKind.Right), -40, 0, -1, 20, 12, 40)]
    [InlineData(nameof(HandleKind.Left), 40, 0, 39, 20, 12, 40)]
    [InlineData(nameof(HandleKind.Bottom), 0, -50, 10, 9, 30, 12)]
    [InlineData(nameof(HandleKind.Top), 0, 50, 10, 59, 30, 12)]
    [InlineData(nameof(HandleKind.BottomRight), -40, -50, -1, 9, 12, 12)]
    [InlineData(nameof(HandleKind.TopLeft), 40, 50, 39, 59, 12, 12)]
    public void Resize_DraggedPastOppositeEdge_FlipsAndKeepsTheOppositeEdgePixel(string handle, int dx, int dy, int x, int y, int width, int height)
    {
        var original = Ellipse(new PixelRect(10, 20, 30, 40), strokeWidth: 3);

        var resized = original.Resize(Enum.Parse<HandleKind>(handle), dx, dy);

        Assert.Equal(original with { Rect = new PixelRect(x, y, width, height) }, resized);
    }

    [Theory]
    [InlineData(nameof(HandleKind.Right), -29, 0, 10, 20, 1, 40)]
    [InlineData(nameof(HandleKind.Left), 29, 0, 39, 20, 1, 40)]
    [InlineData(nameof(HandleKind.Bottom), 0, -39, 10, 20, 30, 1)]
    [InlineData(nameof(HandleKind.Top), 0, 39, 10, 59, 30, 1)]
    [InlineData(nameof(HandleKind.BottomRight), -29, -39, 10, 20, 1, 1)]
    [InlineData(nameof(HandleKind.TopLeft), 29, 39, 39, 59, 1, 1)]
    public void Resize_DraggedOntoOppositeEdge_KeepsAtLeastOnePixel(string handle, int dx, int dy, int x, int y, int width, int height)
    {
        var original = Rectangle(new PixelRect(10, 20, 30, 40), strokeWidth: 3);

        var resized = original.Resize(Enum.Parse<HandleKind>(handle), dx, dy);

        Assert.Equal(original with { Rect = new PixelRect(x, y, width, height) }, resized);
    }

    [Theory]
    [InlineData(7, 7, 3, true)]
    [InlineData(6, 7, 3, false)] // 寬度等於兩倍線寬
    [InlineData(7, 6, 3, false)] // 高度等於兩倍線寬
    [InlineData(3, 3, 1, true)]
    [InlineData(2, 30, 1, false)]
    [InlineData(100, 60, 4, true)]
    public void IsHollow_RequiresWidthAndHeightGreaterThanTwiceStroke(int width, int height, int strokeWidth, bool expected)
    {
        Assert.Equal(expected, Rectangle(new PixelRect(0, 0, width, height), strokeWidth).IsHollow);
        Assert.Equal(expected, Ellipse(new PixelRect(0, 0, width, height), strokeWidth).IsHollow);
    }

    // 矩形框 (10,10)–(110,70)、線寬 4、容差 2：筆劃帶為外緣往內 4 像素，空心內側為 x 14–106、y 14–66。
    [Theory]
    [InlineData(12, 40, true)] // 左側筆劃
    [InlineData(108, 40, true)] // 右側筆劃
    [InlineData(60, 12, true)] // 上側筆劃
    [InlineData(60, 68, true)] // 下側筆劃
    [InlineData(10, 10, true)] // 左上外角
    [InlineData(110, 70, true)] // 右下外角
    [InlineData(15.5, 40, true)] // 空心內側，距筆劃 1.5 像素，在容差內
    [InlineData(8.5, 40, true)] // 外側，距外緣 1.5 像素，在容差內
    [InlineData(60, 40, false)] // 空心中央
    [InlineData(17, 40, false)] // 空心內側，距筆劃 3 像素
    [InlineData(60, 63, false)]
    [InlineData(7, 40, false)] // 外側，距外緣 3 像素
    [InlineData(113, 40, false)]
    [InlineData(60, 73, false)]
    public void HitTest_HollowRectangle_HitsOnlyTheStrokeBandWithinTolerance(double x, double y, bool expected)
    {
        var rectangle = Rectangle(new PixelRect(10, 10, 100, 60), strokeWidth: 4);

        Assert.Equal(expected, rectangle.HitTest(new Point(x, y), tolerance: 2));
    }

    [Theory]
    [InlineData(6, 6)] // 寬高都不超過兩倍線寬
    [InlineData(8, 40)] // 寬度恰為兩倍線寬
    public void HitTest_NonHollowRectangle_HitsAnywhereInside(int width, int height)
    {
        var rectangle = Rectangle(new PixelRect(10, 10, width, height), strokeWidth: 4);

        Assert.False(rectangle.IsHollow);
        Assert.True(rectangle.HitTest(new Point(10 + width / 2.0, 10 + height / 2.0), tolerance: 0));
    }

    // 橢圓範圍 (0,0)–(100,60)、線寬 4、容差 1：中心 (50,30)，外緣半軸 50×30，內緣半軸 46×26。
    [Theory]
    [InlineData(50, 2, true)] // 上側筆劃
    [InlineData(50, 58, true)] // 下側筆劃
    [InlineData(2, 30, true)] // 左側筆劃
    [InlineData(98, 30, true)] // 右側筆劃
    [InlineData(83.94, 49.8, true)] // 45° 方向、位於筆劃中線上
    [InlineData(50, -0.5, true)] // 外緣外 0.5 像素，在容差內
    [InlineData(50, 4.5, true)] // 內緣內 0.5 像素，在容差內
    [InlineData(50, 30, false)] // 中心
    [InlineData(50, 20, false)] // 空心內側
    [InlineData(50, -2, false)] // 外緣外 2 像素
    [InlineData(-2, 30, false)]
    [InlineData(1, 1, false)] // 外接矩形的角落不在橢圓上
    public void HitTest_HollowEllipse_HitsOnlyTheOutlineWithinTolerance(double x, double y, bool expected)
    {
        var ellipse = Ellipse(new PixelRect(0, 0, 100, 60), strokeWidth: 4);

        Assert.Equal(expected, ellipse.HitTest(new Point(x, y), tolerance: 1));
    }

    [Fact]
    public void HitTest_SolidEllipse_HitsAtCenterAndMissesOutside()
    {
        var ellipse = Ellipse(new PixelRect(0, 0, 8, 8), strokeWidth: 4);

        Assert.False(ellipse.IsHollow);
        Assert.True(ellipse.HitTest(new Point(4, 4), tolerance: 0));
        Assert.False(ellipse.HitTest(new Point(4, -2), tolerance: 0));
    }

    [Fact]
    public void EndpointsAndHandles_Segment_ArePixelCenters()
    {
        var line = Line(new PixelPoint(10, 20), new PixelPoint(50, 30), strokeWidth: 2);

        Assert.Equal(new Point(10.5, 20.5), line.StartPoint);
        Assert.Equal(new Point(50.5, 30.5), line.EndPoint);
        Assert.Equal(
            new AnnotationHandle[] { new(HandleKind.Start, new Point(10.5, 20.5)), new(HandleKind.End, new Point(50.5, 30.5)) },
            line.Handles);
    }

    // 水平線段 (10,10)→(50,10)、線寬 4：繪製時通過像素中心 y = 10.5，命中距離為線寬一半 2 加容差。
    [Theory]
    [InlineData(10.5, 10.5, 0, true)] // 起點像素中心
    [InlineData(50.5, 10.5, 0, true)] // 終點像素中心
    [InlineData(30, 12.4, 0, true)] // 距中心線 1.9
    [InlineData(30, 8.3, 0, false)] // 距中心線 2.2；若線段通過像素角點 y = 10 則會命中
    [InlineData(30, 13.4, 1, true)] // 距中心線 2.9
    [InlineData(30, 7.6, 1, true)]
    [InlineData(53.4, 10.5, 1, true)] // 終點外 2.9
    [InlineData(30, 14, 1, false)] // 距中心線 3.5
    [InlineData(30, 7, 1, false)]
    [InlineData(54, 10.5, 1, false)] // 終點外 3.5
    [InlineData(7, 10.5, 1, false)] // 起點外 3.5
    [InlineData(30, 40, 1, false)]
    public void HitTest_Line_HitsWithinHalfStrokePlusToleranceOfPixelCenterSegment(double x, double y, double tolerance, bool expected)
    {
        var line = Line(new PixelPoint(10, 10), new PixelPoint(50, 10), strokeWidth: 4);

        Assert.Equal(expected, line.HitTest(new Point(x, y), tolerance));
    }

    [Fact]
    public void Head_ArrowWithSameStartAndEnd_IsNull()
    {
        var arrow = Arrow(new PixelPoint(10, 10), new PixelPoint(10, 10), strokeWidth: 4);

        Assert.Null(arrow.Head);
        Assert.True(arrow.HitTest(new Point(10.5, 10.5), tolerance: 0));
    }

    [Theory]
    [InlineData(100, 0, 1, 12)] // 頭部至少 12
    [InlineData(100, 0, 3, 12)] // 4 × 線寬 = 12
    [InlineData(100, 0, 4, 16)] // 4 × 線寬
    [InlineData(100, 0, 10, 40)]
    [InlineData(30, 40, 2, 12)] // 斜向，線段長 50
    [InlineData(5, 0, 2, 5)] // 線段比頭部短：頭部長度等於線段長度
    [InlineData(3, 4, 2, 5)]
    [InlineData(30, 0, 10, 30)]
    public void Head_Length_IsShaftLengthCappedByMaxOf12And4TimesStroke(int endX, int endY, int strokeWidth, double expectedLength)
    {
        var arrow = Arrow(new PixelPoint(0, 0), new PixelPoint(endX, endY), strokeWidth);

        var head = Assert.NotNull(arrow.Head);

        Assert.Equal(arrow.EndPoint, head.Tip);
        var baseCenter = new Point((head.Left.X + head.Right.X) / 2, (head.Left.Y + head.Right.Y) / 2);
        Assert.Equal(expectedLength, (head.Tip - baseCenter).Length, 9);
    }

    [Fact]
    public void HitTest_Arrow_PointInsideHeadTriangle_Hits()
    {
        // 線寬 2 的水平箭頭：頭部長 12、底邊位於 x = 88.5。點 (90, 4.5) 距線身 4 像素，只落在頭部三角形內。
        var arrow = Arrow(new PixelPoint(0, 0), new PixelPoint(100, 0), strokeWidth: 2);
        var line = Line(new PixelPoint(0, 0), new PixelPoint(100, 0), strokeWidth: 2);
        var insideHead = new Point(90, 4.5);

        Assert.True(arrow.HitTest(insideHead, tolerance: 0));
        Assert.False(line.HitTest(insideHead, tolerance: 0));
        Assert.False(arrow.HitTest(new Point(90, 7), tolerance: 0.5)); // 頭部斜邊外約 1.1 像素
    }

    [Fact]
    public void Bounds_Arrow_IncludesHeadBeyondShaftStroke()
    {
        var arrow = Arrow(new PixelPoint(0, 0), new PixelPoint(100, 0), strokeWidth: 2);
        var line = Line(new PixelPoint(0, 0), new PixelPoint(100, 0), strokeWidth: 2);
        var head = Assert.NotNull(arrow.Head);

        Assert.Equal(new Rect(-0.5, -0.5, 102, 2), line.Bounds); // 像素中心連線往外擴線寬的一半
        Assert.True(arrow.Bounds.Contains(line.Bounds));
        Assert.True(arrow.Bounds.Contains(head.Tip));
        Assert.True(arrow.Bounds.Contains(head.Left));
        Assert.True(arrow.Bounds.Contains(head.Right));
        Assert.True(arrow.Bounds.Height > line.Bounds.Height);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resize_SegmentEndpointHandle_MovesOnlyThatEndpoint(bool isArrow)
    {
        SegmentAnnotation segment = isArrow
            ? Arrow(new PixelPoint(10, 20), new PixelPoint(50, 30), strokeWidth: 2)
            : Line(new PixelPoint(10, 20), new PixelPoint(50, 30), strokeWidth: 2);

        Assert.Equal(segment with { Start = new PixelPoint(13, 18) }, segment.Resize(HandleKind.Start, 3, -2));
        Assert.Equal(segment with { End = new PixelPoint(53, 28) }, segment.Resize(HandleKind.End, 3, -2));
        Assert.Equal(segment, segment.Resize(HandleKind.BottomRight, 3, -2)); // 矩形範圍的控制點對線段無作用
    }

    [Fact]
    public void Translate_Segment_MovesBothEndpoints()
    {
        var arrow = Arrow(new PixelPoint(10, 20), new PixelPoint(50, 30), strokeWidth: 2);

        Assert.Equal(arrow with { Start = new PixelPoint(15, 13), End = new PixelPoint(55, 23) }, arrow.Translate(5, -7));
    }

    [Fact]
    public void BoundsAndHandles_StepNumber_AreSquareAroundCenterPixel()
    {
        var step = Step(new PixelPoint(50, 50), radius: 10);

        Assert.Equal(new Point(50.5, 50.5), step.CenterPoint);
        Assert.Equal(new Rect(40.5, 40.5, 20, 20), step.Bounds);
        Assert.Equal(
            new AnnotationHandle[]
            {
                new(HandleKind.TopLeft, new Point(40.5, 40.5)),
                new(HandleKind.TopRight, new Point(60.5, 40.5)),
                new(HandleKind.BottomRight, new Point(60.5, 60.5)),
                new(HandleKind.BottomLeft, new Point(40.5, 60.5)),
            },
            step.Handles);
    }

    // 圓心 (50.5, 50.5)、半徑 10 的實心圓盤。
    [Theory]
    [InlineData(50.5, 50.5, 0, true)] // 圓心
    [InlineData(60.5, 50.5, 0, true)] // 圓周
    [InlineData(57.5, 57.5, 0, true)] // 斜向，距圓心 9.9
    [InlineData(57.6, 57.6, 0, false)] // 斜向，距圓心 10.04
    [InlineData(61.5, 50.5, 1, true)] // 圓周外 1，在容差內
    [InlineData(61.5, 50.5, 0.5, false)]
    [InlineData(40.5, 40.5, 0, false)] // 外接正方形的角落
    public void HitTest_StepNumber_HitsWithinRadiusPlusTolerance(double x, double y, double tolerance, bool expected)
    {
        var step = Step(new PixelPoint(50, 50), radius: 10);

        Assert.Equal(expected, step.HitTest(new Point(x, y), tolerance));
    }

    // 沿對角線拖曳角落 d 像素，外接正方形的角落跟著移動 d，半徑也改變 d。
    [Theory]
    [InlineData(nameof(HandleKind.BottomRight), 4, 4, 24)] // 往外：放大
    [InlineData(nameof(HandleKind.TopLeft), -4, -4, 24)]
    [InlineData(nameof(HandleKind.TopRight), 4, -4, 24)]
    [InlineData(nameof(HandleKind.BottomLeft), -4, 4, 24)]
    [InlineData(nameof(HandleKind.BottomRight), -4, -4, 16)] // 往內：縮小
    [InlineData(nameof(HandleKind.TopLeft), 4, 4, 16)]
    [InlineData(nameof(HandleKind.TopRight), -4, 4, 16)]
    [InlineData(nameof(HandleKind.BottomLeft), 4, -4, 16)]
    [InlineData(nameof(HandleKind.Top), 4, 4, 20)] // 不是角落：不變
    [InlineData(nameof(HandleKind.End), 4, 4, 20)]
    public void Resize_StepNumberCorner_GrowsOutwardAndShrinksInward(string handle, int dx, int dy, int expectedRadius)
    {
        var step = Step(new PixelPoint(50, 50), radius: 20);

        var resized = step.Resize(Enum.Parse<HandleKind>(handle), dx, dy);

        Assert.Equal(step with { Radius = expectedRadius }, resized);
    }

    [Theory]
    [InlineData(-100, -100, StepNumberAnnotation.MinRadius)]
    [InlineData(2000, 2000, StepNumberAnnotation.MaxRadius)]
    public void Resize_StepNumberCorner_ClampsRadiusToLimits(int dx, int dy, int expectedRadius)
    {
        var step = Step(new PixelPoint(50, 50), radius: 20);

        Assert.Equal(step with { Radius = expectedRadius }, step.Resize(HandleKind.BottomRight, dx, dy));
    }

    [Theory]
    [InlineData(16, 12)]
    [InlineData(18, 14)] // 13.5 四捨五入，中點遠離零
    [InlineData(10, 8)] // 7.5
    [InlineData(24, 18)]
    [InlineData(72, 54)]
    [InlineData(4, StepNumberAnnotation.MinRadius)]
    [InlineData(1000, StepNumberAnnotation.MaxRadius)]
    public void DefaultRadius_FollowsFontSizeWithinRadiusLimits(int fontSize, int expected)
    {
        Assert.Equal(expected, StepNumberAnnotation.DefaultRadius(fontSize));
    }

    [Fact]
    public void HandlesAndResize_Text_HasNoHandlesAndResizeReturnsItself()
    {
        var text = Text(new PixelPoint(10, 20), "標註");

        Assert.Empty(text.Handles);
        Assert.Same(text, text.Resize(HandleKind.BottomRight, 5, 5));
        Assert.Same(text, text.Resize(HandleKind.End, 5, 5));
    }

    [Fact]
    public void Translate_Text_MovesOrigin()
    {
        var text = Text(new PixelPoint(10, 20), "標註");

        Assert.Equal(text with { Origin = new PixelPoint(15, 13) }, text.Translate(5, -7));
    }

    [Fact]
    public void Bounds_Text_StartsAtOriginAndGrowsWithLinesAndLength()
    {
        var singleLine = Text(new PixelPoint(10, 20), "第一行").Bounds;
        var twoLines = Text(new PixelPoint(10, 20), "第一行\n第二行").Bounds;
        var shortText = Text(new PixelPoint(10, 20), "Hi").Bounds;
        var longText = Text(new PixelPoint(10, 20), "Hi there, a longer line").Bounds;

        Assert.Equal(10, singleLine.X);
        Assert.Equal(20, singleLine.Y);
        Assert.True(singleLine.Width > 0);
        Assert.True(twoLines.Height > singleLine.Height * 1.5);
        Assert.True(longText.Width > shortText.Width);
    }

    [Fact]
    public void HitTest_Text_HitsWithinBoundsPlusTolerance()
    {
        var text = Text(new PixelPoint(10, 20), "標註文字");
        var bounds = text.Bounds;

        Assert.True(text.HitTest(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), tolerance: 0));
        Assert.True(text.HitTest(new Point(bounds.Right + 1.5, bounds.Y + 1), tolerance: 2));
        Assert.False(text.HitTest(new Point(bounds.Right + 3, bounds.Y + 1), tolerance: 2));
    }

    [Theory]
    [InlineData(10, 20, 20, 25)]
    [InlineData(20, 25, 10, 20)]
    [InlineData(10, 25, 20, 20)]
    [InlineData(20, 20, 10, 25)]
    public void Spanning_AnyOrder_IncludesBothPixels(int ax, int ay, int bx, int by)
    {
        Assert.Equal(new PixelRect(10, 20, 11, 6), BoxHandles.Spanning(new PixelPoint(ax, ay), new PixelPoint(bx, by)));
    }

    [Fact]
    public void Spanning_SamePixel_IsOneByOne()
    {
        Assert.Equal(new PixelRect(7, 8, 1, 1), BoxHandles.Spanning(new PixelPoint(7, 8), new PixelPoint(7, 8)));
    }

    // 範圍 (10,10,20,20) 限制在 (0,0,50,40) 內：移動的邊停在限制範圍的最外圍像素。
    [Theory]
    [InlineData(nameof(HandleKind.Right), 100, 0, 10, 10, 40, 20)]
    [InlineData(nameof(HandleKind.Left), -100, 0, 0, 10, 30, 20)]
    [InlineData(nameof(HandleKind.Bottom), 0, 100, 10, 10, 20, 30)]
    [InlineData(nameof(HandleKind.Top), 0, -100, 10, 0, 20, 30)]
    [InlineData(nameof(HandleKind.BottomRight), 100, 100, 10, 10, 40, 30)]
    [InlineData(nameof(HandleKind.TopLeft), -100, -100, 0, 0, 30, 30)]
    [InlineData(nameof(HandleKind.Right), -100, 0, 0, 10, 11, 20)] // 拖過對邊翻轉，仍在限制範圍內
    [InlineData(nameof(HandleKind.Right), 5, 0, 10, 10, 25, 20)] // 未超出限制時不受影響
    public void Resize_WithLimit_ClampsMovedEdgesInsideLimit(string handle, int dx, int dy, int x, int y, int width, int height)
    {
        var limit = new PixelRect(0, 0, 50, 40);

        var resized = BoxHandles.Resize(new PixelRect(10, 10, 20, 20), Enum.Parse<HandleKind>(handle), dx, dy, limit);

        Assert.Equal(new PixelRect(x, y, width, height), resized);
        Assert.True(limit.Contains(resized));
    }

    [Theory]
    [InlineData(5, 5, 0, 0, 10, 0, 5)] // 垂足落在線段上
    [InlineData(5, 0, 0, 0, 10, 0, 0)] // 點在線段上
    [InlineData(13, 4, 0, 0, 10, 0, 5)] // 超出終點：距終點
    [InlineData(-3, -4, 0, 0, 10, 0, 5)] // 超出起點：距起點
    [InlineData(3, 4, 0, 0, 0, 0, 5)] // 線段退化為一點
    [InlineData(0, 10, 0, 0, 10, 10, 7.0710678118654755)]
    public void DistanceToSegment_ReturnsDistanceToNearestPointOnSegment(double px, double py, double ax, double ay, double bx, double by, double expected)
    {
        Assert.Equal(expected, Geometry2D.DistanceToSegment(new Point(px, py), new Point(ax, ay), new Point(bx, by)), 9);
    }

    // 三角形 (0,0)、(10,0)、(0,10)，以兩種頂點順序驗證。
    [Theory]
    [InlineData(2, 2, 0)] // 內部
    [InlineData(5, 0, 0)] // 邊上
    [InlineData(0, 10, 0)] // 頂點
    [InlineData(-3, 5, 3)] // 左側
    [InlineData(5, -4, 4)] // 下方
    [InlineData(10, 10, 7.0710678118654755)] // 斜邊外側
    [InlineData(12, -5, 5.385164807134504)] // 最近的是頂點 (10,0)
    public void DistanceToTriangle_EitherWinding_IsZeroInsideAndNearestEdgeDistanceOutside(double px, double py, double expected)
    {
        var point = new Point(px, py);

        Assert.Equal(expected, Geometry2D.DistanceToTriangle(point, new Point(0, 0), new Point(10, 0), new Point(0, 10)), 9);
        Assert.Equal(expected, Geometry2D.DistanceToTriangle(point, new Point(0, 0), new Point(0, 10), new Point(10, 0)), 9);
    }

    [Fact]
    public void EditorSessionHitTest_OverlappingAnnotations_ReturnsTopmost()
    {
        var session = CreateSession();
        var lower = AddAnnotation(session, Step(new PixelPoint(60, 40), radius: 10));
        var upper = AddAnnotation(session, Step(new PixelPoint(64, 40), radius: 10));

        Assert.Same(upper, session.HitTest(new Point(62.5, 40.5), tolerance: 0)); // 兩者重疊處
        Assert.Same(lower, session.HitTest(new Point(52.5, 40.5), tolerance: 0)); // 只有下層
        Assert.Null(session.HitTest(new Point(150.5, 80.5), tolerance: 0));
    }

    [Fact]
    public void EditorSessionHitTest_PointInsideHollowOfUpperRectangle_ReturnsLowerAnnotation()
    {
        var session = CreateSession();
        var lower = AddAnnotation(session, Step(new PixelPoint(60, 40), radius: 10));
        var upper = AddAnnotation(session, Rectangle(new PixelRect(10, 10, 100, 60), strokeWidth: 4));

        Assert.Same(lower, session.HitTest(new Point(60.5, 40.5), tolerance: 2)); // 框內、下層的步驟編號上
        Assert.Same(upper, session.HitTest(new Point(12, 40), tolerance: 2)); // 上層的筆劃
        Assert.Null(session.HitTest(new Point(30, 25), tolerance: 2)); // 框內、下層沒有標註
    }

    private static RectangleAnnotation Rectangle(PixelRect rect, int strokeWidth) =>
        new() { Color = Red, Rect = rect, StrokeWidth = strokeWidth };

    private static EllipseAnnotation Ellipse(PixelRect rect, int strokeWidth) =>
        new() { Color = Red, Rect = rect, StrokeWidth = strokeWidth };

    private static LineAnnotation Line(PixelPoint start, PixelPoint end, int strokeWidth) =>
        new() { Color = Red, Start = start, End = end, StrokeWidth = strokeWidth };

    private static ArrowAnnotation Arrow(PixelPoint start, PixelPoint end, int strokeWidth) =>
        new() { Color = Red, Start = start, End = end, StrokeWidth = strokeWidth };

    private static StepNumberAnnotation Step(PixelPoint center, int radius) =>
        new() { Color = Red, Center = center, Radius = radius, Number = 1 };

    private static TextAnnotation Text(PixelPoint origin, string text) =>
        new() { Color = Red, Origin = origin, Text = text, FontFamily = "Segoe UI", FontSize = 16 };

    private static EditorSession CreateSession()
    {
        const int width = 200;
        const int height = 100;
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, new byte[width * height * 4], width * 4);
        source.Freeze();
        return new EditorSession(new ImageDocument(source), new AnnotationStyle(Red, 3, "Segoe UI", 16));
    }

    private static T AddAnnotation<T>(EditorSession session, T annotation)
        where T : Annotation
    {
        session.Document.History.Execute(new AddAnnotationCommand(session.Document, annotation));
        return annotation;
    }
}
