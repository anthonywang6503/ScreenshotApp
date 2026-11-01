namespace ScreenshotApp.Capture;

/// <summary>覆蓋層完成後交付的結果；取色不包含圖片或選取範圍。</summary>
internal abstract record OverlayResult;

internal sealed record RegionSelected(SelectionResult Selection) : OverlayResult;

internal sealed record ColorPicked(string ColorCode) : OverlayResult;
