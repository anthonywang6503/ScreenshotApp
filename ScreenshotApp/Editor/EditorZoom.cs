namespace ScreenshotApp.Editor;

/// <summary>
/// 編輯畫布的縮放階段與「符合視窗」計算。縮放比例為每個圖片像素占幾個實體像素（1 即 100%）。
/// </summary>
internal static class EditorZoom
{
    // 符合視窗的下限，避免視窗極小時比例趨近於 0。
    private const double MinFit = 0.01;

    // 判斷縮放比例是否等於某個階段時容許的誤差。
    private const double Tolerance = 1e-9;

    public static IReadOnlyList<double> Steps { get; } = [0.25, 1.0 / 3, 0.5, 2.0 / 3, 1, 1.5, 2, 3, 4];

    /// <summary>
    /// 可用空間（實體像素）放得下整個範圍的最大縮放；超大圖片可低於最小階段。預設不超過 100%，小圖維持原尺寸；
    /// <paramref name="enlarge"/> 為 true 時（畫布只顯示裁切範圍）可放大到最大階段，讓範圍填滿畫布。
    /// </summary>
    public static double Fit(double availableWidth, double availableHeight, int contentWidth, int contentHeight, bool enlarge = false)
    {
        var fit = Math.Min(availableWidth / contentWidth, availableHeight / contentHeight);
        return double.IsFinite(fit) ? Math.Clamp(fit, MinFit, enlarge ? Steps[^1] : 1) : 1;
    }

    /// <summary>下一個較大的階段；已達最大階段時維持最大階段。</summary>
    public static double ZoomIn(double zoom) => Steps.FirstOrDefault(step => step > zoom + Tolerance, Steps[^1]);

    /// <summary>下一個較小的階段；已小於最小階段（符合超大圖片時）則維持不變。</summary>
    public static double ZoomOut(double zoom) => Steps.LastOrDefault(step => step < zoom - Tolerance, Math.Min(zoom, Steps[0]));
}
