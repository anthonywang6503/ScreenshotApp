using System.Windows.Media;
using ScreenshotApp.Resources;

namespace ScreenshotApp.Settings;

/// <summary>帶有顯示名稱的顏色；名稱讓顏色不是辨識選項的唯一方式。</summary>
internal sealed record NamedColor(Color Color, string Name)
{
    public override string ToString() => Name;
}

/// <summary>常用標註顏色。</summary>
internal static class CommonColors
{
    public static IReadOnlyList<NamedColor> All { get; } =
    [
        new(Color.FromRgb(0xFF, 0x00, 0x00), Strings.Get("Color_Red")),
        new(Color.FromRgb(0xFF, 0x8C, 0x00), Strings.Get("Color_Orange")),
        new(Color.FromRgb(0xFF, 0xD4, 0x00), Strings.Get("Color_Yellow")),
        new(Color.FromRgb(0x00, 0xB0, 0x50), Strings.Get("Color_Green")),
        new(Color.FromRgb(0x00, 0x78, 0xD4), Strings.Get("Color_Blue")),
        new(Color.FromRgb(0x88, 0x17, 0x98), Strings.Get("Color_Purple")),
        new(Color.FromRgb(0x00, 0x00, 0x00), Strings.Get("Color_Black")),
        new(Color.FromRgb(0xFF, 0xFF, 0xFF), Strings.Get("Color_White")),
    ];

    public static NamedColor Custom(Color color) => new(color, Strings.Format("Color_Custom", ColorText.Format(color)));
}
