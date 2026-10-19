using ScreenshotApp.Editor;

namespace ScreenshotApp.Tests;

/// <summary>
/// 編輯畫布的縮放：符合視窗的比例（上限 100%、只顯示裁切範圍時可放大到最大階段、超大圖片可低於最小階段、
/// 可用空間異常時的下限與預設值），以及在固定縮放階段之間逐階放大與縮小。
/// </summary>
public class EditorZoomTests
{
    private static readonly double[] ExpectedSteps = [0.25, 1.0 / 3, 0.5, 2.0 / 3, 1, 1.5, 2, 3, 4];

    [Fact]
    public void Steps_AreTheZoomLevelsFrom25To400PercentInAscendingOrder()
    {
        Assert.Equal(ExpectedSteps, EditorZoom.Steps);
    }

    [Theory]
    [InlineData(800.0, 600.0, 1600, 1200, 0.5)]
    [InlineData(800.0, 600.0, 1000, 500, 0.8)] // 寬度先受限
    [InlineData(800.0, 600.0, 400, 1200, 0.5)] // 高度先受限
    [InlineData(1000.0, 1000.0, 8000, 4000, 0.125)] // 超大圖片可低於最小階段
    [InlineData(1920.0, 1080.0, 800, 600, 1.0)] // 小圖維持 100%，不放大
    [InlineData(800.0, 600.0, 800, 600, 1.0)] // 剛好放得下
    public void Fit_ReturnsLargestZoomThatFitsCappedAt100Percent(
        double availableWidth, double availableHeight, int contentWidth, int contentHeight, double expected)
    {
        Assert.Equal(expected, EditorZoom.Fit(availableWidth, availableHeight, contentWidth, contentHeight), 1e-12);
    }

    // 只顯示裁切範圍時允許放大，讓範圍填滿畫布；放大以最大階段為上限，比可用空間大的範圍與不放大時相同。
    [Theory]
    [InlineData(800.0, 600.0, 100, 50, 4.0)] // 很小的範圍停在最大階段
    [InlineData(800.0, 600.0, 400, 200, 2.0)] // 寬度先受限
    [InlineData(800.0, 600.0, 300, 400, 1.5)] // 高度先受限
    [InlineData(1920.0, 1080.0, 800, 600, 1.8)] // 不放大時維持 100% 的小圖
    [InlineData(800.0, 600.0, 1600, 1200, 0.5)]
    [InlineData(1000.0, 1000.0, 8000, 4000, 0.125)]
    public void Fit_Enlarge_FillsAvailableSpaceUpToLargestStep(
        double availableWidth, double availableHeight, int contentWidth, int contentHeight, double expected)
    {
        Assert.Equal(expected, EditorZoom.Fit(availableWidth, availableHeight, contentWidth, contentHeight, enlarge: true), 1e-12);
    }

    [Theory]
    [InlineData(0.0, 600.0)]
    [InlineData(800.0, 0.0)]
    [InlineData(-100.0, 600.0)]
    [InlineData(800.0, -1.0)]
    public void Fit_NonPositiveSpace_ClampsToSmallPositiveMinimum(double availableWidth, double availableHeight)
    {
        var fit = EditorZoom.Fit(availableWidth, availableHeight, 800, 600);

        Assert.True(fit > 0, $"符合視窗的比例應大於 0，實際為 {fit}。");
        Assert.True(fit < ExpectedSteps[0], $"符合視窗的比例應小於最小階段，實際為 {fit}。");
        Assert.Equal(EditorZoom.Fit(0, 0, 800, 600), fit);
    }

    [Theory]
    [InlineData(double.NaN, 600.0)]
    [InlineData(800.0, double.NaN)]
    [InlineData(double.PositiveInfinity, double.PositiveInfinity)]
    public void Fit_NonFiniteSpace_Returns100Percent(double availableWidth, double availableHeight)
    {
        Assert.Equal(1.0, EditorZoom.Fit(availableWidth, availableHeight, 800, 600));
    }

    [Fact]
    public void ZoomIn_FromSmallestStep_VisitsEveryStepThenStaysAtLargest()
    {
        var zoom = ExpectedSteps[0];

        for (var i = 1; i < ExpectedSteps.Length; i++)
        {
            zoom = EditorZoom.ZoomIn(zoom);
            Assert.Equal(ExpectedSteps[i], zoom, 1e-9);
        }

        Assert.Equal(4.0, EditorZoom.ZoomIn(zoom), 1e-9);
        Assert.Equal(4.0, EditorZoom.ZoomIn(4.0), 1e-9);
    }

    [Fact]
    public void ZoomOut_FromLargestStep_VisitsEveryStepThenStaysAtSmallest()
    {
        var zoom = ExpectedSteps[^1];

        for (var i = ExpectedSteps.Length - 2; i >= 0; i--)
        {
            zoom = EditorZoom.ZoomOut(zoom);
            Assert.Equal(ExpectedSteps[i], zoom, 1e-9);
        }

        Assert.Equal(0.25, EditorZoom.ZoomOut(zoom), 1e-9);
        Assert.Equal(0.25, EditorZoom.ZoomOut(0.25), 1e-9);
    }

    [Theory]
    [InlineData(0.45, 0.5, 1.0 / 3)]
    [InlineData(0.3, 1.0 / 3, 0.25)]
    [InlineData(0.7, 1.0, 2.0 / 3)]
    [InlineData(1.2, 1.5, 1.0)]
    [InlineData(3.5, 4.0, 3.0)]
    public void ZoomInAndOut_FromZoomBetweenSteps_GoToNextLargerOrSmallerStep(double zoom, double expectedIn, double expectedOut)
    {
        Assert.Equal(expectedIn, EditorZoom.ZoomIn(zoom), 1e-9);
        Assert.Equal(expectedOut, EditorZoom.ZoomOut(zoom), 1e-9);
    }

    [Theory]
    [InlineData(2.0 / 3 - 1e-12, 1.0, 0.5)]
    [InlineData(1.0 / 3 + 1e-12, 0.5, 0.25)]
    [InlineData(1.5 + 1e-12, 2.0, 1.0)]
    public void ZoomInAndOut_WithinFloatingErrorOfStep_TreatZoomAsThatStep(double zoom, double expectedIn, double expectedOut)
    {
        Assert.Equal(expectedIn, EditorZoom.ZoomIn(zoom), 1e-9);
        Assert.Equal(expectedOut, EditorZoom.ZoomOut(zoom), 1e-9);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.01)]
    [InlineData(0.2)]
    public void ZoomOut_BelowSmallestStep_KeepsZoomUnchanged(double zoom)
    {
        Assert.Equal(zoom, EditorZoom.ZoomOut(zoom));
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.01)]
    public void ZoomIn_BelowSmallestStep_GoesToSmallestStep(double zoom)
    {
        Assert.Equal(0.25, EditorZoom.ZoomIn(zoom), 1e-9);
    }
}
