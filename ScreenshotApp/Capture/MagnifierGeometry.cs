using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Capture;

internal readonly record struct MagnifierLayout(PixelRect Sample, PixelRect Panel);

/// <summary>放大鏡的取樣與面板位置，全部使用顯示器本地實體像素。</summary>
internal static class MagnifierGeometry
{
    public const int SampleCount = 15;
    public const int CellPixels = 12;
    public const int CursorGap = 24;

    public static MagnifierLayout Calculate(int width, int height, PixelPoint cursor,
        int sampleCount, int cellPixels, int labelWidth, int labelHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellPixels);
        if (sampleCount % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleCount), "取樣格數必須為奇數。");
        }

        var gridSize = checked(sampleCount * cellPixels);
        var panelWidth = Math.Max(gridSize, labelWidth);
        var panelHeight = gridSize + labelHeight;
        var x = Place(cursor.X, panelWidth, width);
        var y = Place(cursor.Y, panelHeight, height);
        return new MagnifierLayout(
            new PixelRect(cursor.X - sampleCount / 2, cursor.Y - sampleCount / 2, sampleCount, sampleCount),
            new PixelRect(x, y, panelWidth, panelHeight));
    }

    private static int Place(int cursor, int size, int extent)
    {
        var position = cursor + CursorGap;
        if (position + size > extent)
        {
            position = cursor - CursorGap - size;
        }

        return Math.Clamp(position, 0, Math.Max(0, extent - size));
    }
}
