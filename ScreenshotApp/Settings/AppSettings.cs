using System.Windows.Media;

namespace ScreenshotApp.Settings;

/// <summary>使用者設定。線寬與字級以最終圖片像素為單位。</summary>
internal sealed record AppSettings
{
    public const int MinStrokeWidth = 1;
    public const int MaxStrokeWidth = 50;
    public const int MinFontSize = 8;
    public const int MaxFontSize = 200;

    public HotkeyGesture Hotkey { get; init; } = HotkeyGesture.Default;

    public bool StartWithWindows { get; init; }

    public string? LastSaveDirectory { get; init; }

    public Color DefaultColor { get; init; } = Color.FromRgb(0xFF, 0x00, 0x00);

    public int DefaultStrokeWidth { get; init; } = 3;

    public int DefaultFontSize { get; init; } = 20;
}
