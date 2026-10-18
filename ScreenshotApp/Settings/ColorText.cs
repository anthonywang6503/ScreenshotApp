using System.Globalization;
using System.Windows.Media;

namespace ScreenshotApp.Settings;

/// <summary>顏色與 <c>#RRGGBB</c> 文字之間的轉換。</summary>
internal static class ColorText
{
    public static string Format(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static bool TryParse(string? text, out Color color)
    {
        color = default;
        if (text is not { Length: 7 } || text[0] != '#'
            || !uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }
}
