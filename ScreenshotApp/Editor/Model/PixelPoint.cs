namespace ScreenshotApp.Editor.Model;

/// <summary>整數像素座標的點；屬於桌面或顯示器本地座標空間由使用端決定。</summary>
internal readonly record struct PixelPoint(int X, int Y);
