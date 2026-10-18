using System.Globalization;
using System.Resources;

namespace ScreenshotApp.Resources;

/// <summary>
/// 使用者介面文字的唯一來源。找不到的鍵回傳 <c>!Key!</c>，讓遺漏在畫面上一眼可見，而不是讓程式當掉。
/// </summary>
internal static class Strings
{
    private static readonly ResourceManager Manager =
        new("ScreenshotApp.Resources.Strings", typeof(Strings).Assembly);

    public static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture) ?? $"!{key}!";

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
