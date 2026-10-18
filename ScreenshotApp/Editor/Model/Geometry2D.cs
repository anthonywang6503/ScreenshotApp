using System.Windows;

namespace ScreenshotApp.Editor.Model;

/// <summary>命中判定用的平面幾何。</summary>
internal static class Geometry2D
{
    public static double DistanceToSegment(Point point, Point a, Point b)
    {
        var segment = b - a;
        var lengthSquared = segment.LengthSquared;
        if (lengthSquared < 1e-18)
        {
            return (point - a).Length;
        }

        var t = Math.Clamp((point - a) * segment / lengthSquared, 0, 1);
        return (point - (a + t * segment)).Length;
    }

    /// <summary>點到實心三角形的距離；在三角形內為 0。</summary>
    public static double DistanceToTriangle(Point point, Point a, Point b, Point c)
    {
        var d1 = Cross(point, a, b);
        var d2 = Cross(point, b, c);
        var d3 = Cross(point, c, a);
        var hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
        var hasPositive = d1 > 0 || d2 > 0 || d3 > 0;
        if (!(hasNegative && hasPositive))
        {
            return 0;
        }

        return Math.Min(DistanceToSegment(point, a, b), Math.Min(DistanceToSegment(point, b, c), DistanceToSegment(point, c, a)));
    }

    private static double Cross(Point point, Point a, Point b) => (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);
}
