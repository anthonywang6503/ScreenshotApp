using System.Windows.Media;
using ScreenshotApp.Settings;

namespace ScreenshotApp.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_MatchTheSpecifiedInitialValues()
    {
        var settings = new AppSettings();

        Assert.Equal(HotkeyGesture.Default, settings.Hotkey);
        Assert.False(settings.StartWithWindows);
        Assert.Null(settings.LastSaveDirectory);
        Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), settings.DefaultColor);
        Assert.Equal(3, settings.DefaultStrokeWidth);
        Assert.Equal(20, settings.DefaultFontSize);
    }

    [Fact]
    public void DefaultColor_IsTheFirstCommonColor()
    {
        Assert.Equal(CommonColors.All[0].Color, new AppSettings().DefaultColor);
    }

    [Fact]
    public void CommonColors_HaveDistinctColorsAndNames()
    {
        Assert.Equal(8, CommonColors.All.Count);
        Assert.Equal(8, CommonColors.All.Select(color => color.Color).Distinct().Count());
        Assert.Equal(8, CommonColors.All.Select(color => color.Name).Distinct().Count());
        Assert.DoesNotContain(CommonColors.All, color => color.Name.StartsWith('!'));
    }

    [Theory]
    [InlineData("#FF0000", 0xFF, 0x00, 0x00)]
    [InlineData("#00b050", 0x00, 0xB0, 0x50)]
    [InlineData("#000000", 0x00, 0x00, 0x00)]
    public void ColorText_TryParse_AcceptsHashRrggbb(string text, byte r, byte g, byte b)
    {
        Assert.True(ColorText.TryParse(text, out var color));
        Assert.Equal(Color.FromRgb(r, g, b), color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("FF0000")]
    [InlineData("#FF000")]
    [InlineData("#FF00000")]
    [InlineData("#GG0000")]
    [InlineData("#FF000 ")]
    [InlineData("#+F0000")]
    public void ColorText_TryParse_RejectsOtherFormats(string? text)
    {
        Assert.False(ColorText.TryParse(text, out _));
    }

    [Fact]
    public void ColorText_Format_UsesUppercaseHex()
    {
        Assert.Equal("#00B050", ColorText.Format(Color.FromRgb(0x00, 0xB0, 0x50)));
    }
}
