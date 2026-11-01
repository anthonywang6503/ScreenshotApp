using System.Globalization;

namespace ScreenshotApp.Editor.Model;

/// <summary>整數像素中心之間的距離與包含端點的外接範圍。</summary>
internal readonly record struct PixelMeasurement(PixelPoint Start, PixelPoint End)
{
    public double DeltaX => (double)End.X - Start.X;
    public double DeltaY => (double)End.Y - Start.Y;
    public double Length => Math.Sqrt(DeltaX * DeltaX + DeltaY * DeltaY);
    public double Width => Math.Abs(DeltaX) + 1;
    public double Height => Math.Abs(DeltaY) + 1;
    public string DisplayLength => Math.Round(Length, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture);
}
