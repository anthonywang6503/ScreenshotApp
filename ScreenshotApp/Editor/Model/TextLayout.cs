using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ScreenshotApp.Editor.Model;

/// <summary>文字版面：文字標註的範圍計算與繪製碼共用同一份版面，量測結果與畫面一致。</summary>
internal static class TextLayout
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("zh-TW");

    /// <summary>
    /// pixelsPerDip 固定為 1：版面以圖片像素計算，與螢幕 DPI 及畫布縮放無關。只依明確的換行分行，不自動換行。
    /// 文化設為 zh-TW，字型後援才會為中文字元選用繁體字形。
    /// </summary>
    public static FormattedText Create(string text, string fontFamily, double fontSize, Brush foreground) =>
        new(
            text,
            Culture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily(fontFamily), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            fontSize,
            foreground,
            1.0);
}
