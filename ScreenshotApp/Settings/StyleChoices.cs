using ScreenshotApp.Resources;

namespace ScreenshotApp.Settings;

/// <summary>字型名稱與顯示名稱；顯示名稱為台灣慣用的中文名稱。</summary>
internal sealed record NamedFont(string FamilyName, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>標註樣式的選項，設定視窗與編輯器共用。線寬與字級以圖片像素為單位。</summary>
internal static class StyleChoices
{
    public static IReadOnlyList<int> StrokeWidths { get; } = [1, 2, 3, 4, 5, 6, 8, 10, 12, 16, 20];

    public static IReadOnlyList<int> FontSizes { get; } = [10, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 64, 72];

    /// <summary>Windows 內建、支援繁體中文或常用於說明文字的基本字型；第一個為預設值。</summary>
    public static IReadOnlyList<NamedFont> FontFamilies { get; } =
    [
        new("Microsoft JhengHei", Strings.Get("Font_MicrosoftJhengHei")),
        new("PMingLiU", Strings.Get("Font_PMingLiU")),
        new("DFKai-SB", Strings.Get("Font_DFKaiSB")),
        new("Segoe UI", Strings.Get("Font_SegoeUI")),
        new("Consolas", Strings.Get("Font_Consolas")),
    ];

    public static string DefaultFontFamily => FontFamilies[0].FamilyName;

    /// <summary>選項中沒有目前的值時依大小插入，讓清單永遠能顯示目前的值。</summary>
    public static int[] IncludeValue(IReadOnlyList<int> choices, int value) =>
        choices.Contains(value) ? [.. choices] : [.. choices.Append(value).Order()];
}
