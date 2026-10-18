using System.Windows.Input;

namespace ScreenshotApp.Settings;

internal enum HotkeyValidation
{
    Valid,
    MissingModifier,
    UnsupportedKey,
    ReservedBySystem,
}

/// <summary>
/// 全域快捷鍵組合。文字格式為 <c>Ctrl+Shift+2</c>，修飾鍵固定以 Win、Ctrl、Alt、Shift 的順序輸出。
/// WPF 的 <see cref="ModifierKeys"/> 數值與 RegisterHotKey 的修飾鍵旗標相同，可直接傳給 Win32。
/// </summary>
internal readonly record struct HotkeyGesture(ModifierKeys Modifiers, Key Key)
{
    private static readonly (ModifierKeys Modifier, string Name)[] ModifierOrder =
    [
        (ModifierKeys.Windows, "Win"),
        (ModifierKeys.Control, "Ctrl"),
        (ModifierKeys.Alt, "Alt"),
        (ModifierKeys.Shift, "Shift"),
    ];

    private static readonly Dictionary<string, ModifierKeys> ModifierAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Win"] = ModifierKeys.Windows,
        ["Windows"] = ModifierKeys.Windows,
        ["Ctrl"] = ModifierKeys.Control,
        ["Control"] = ModifierKeys.Control,
        ["Alt"] = ModifierKeys.Alt,
        ["Shift"] = ModifierKeys.Shift,
    };

    private static readonly Dictionary<Key, string> KeyNames = BuildKeyNames();

    private static readonly Dictionary<string, Key> KeysByName =
        KeyNames.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    public static HotkeyGesture Default { get; } = new(ModifierKeys.Control | ModifierKeys.Shift, Key.D2);

    public int VirtualKey => KeyInterop.VirtualKeyFromKey(Key);

    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        var modifiers = ModifierKeys.None;
        foreach (var part in parts[..^1])
        {
            if (!ModifierAliases.TryGetValue(part, out var modifier))
            {
                return false;
            }

            modifiers |= modifier;
        }

        if (!KeysByName.TryGetValue(parts[^1], out var key))
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, key);
        return true;
    }

    /// <summary>修飾鍵的顯示文字，例如 <c>Ctrl+Shift</c>；沒有修飾鍵時為空字串。</summary>
    public static string FormatModifiers(ModifierKeys modifiers) =>
        string.Join("+", ModifierOrder.Where(entry => modifiers.HasFlag(entry.Modifier)).Select(entry => entry.Name));

    public HotkeyValidation Validate()
    {
        if (!KeyNames.ContainsKey(Key))
        {
            return HotkeyValidation.UnsupportedKey;
        }

        // 不搶用 Windows 保留的截圖快捷鍵：PrintScreen 系列與 Win+Shift+S。
        if (Key == Key.Snapshot || (Modifiers == (ModifierKeys.Windows | ModifierKeys.Shift) && Key == Key.S))
        {
            return HotkeyValidation.ReservedBySystem;
        }

        // 只有 Shift 會干擾一般打字，因此至少需要 Ctrl、Alt 或 Win。
        if ((Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0)
        {
            return HotkeyValidation.MissingModifier;
        }

        return HotkeyValidation.Valid;
    }

    public override string ToString()
    {
        var modifiers = FormatModifiers(Modifiers);
        var key = KeyNames.TryGetValue(Key, out var name) ? name : Key.ToString();
        return modifiers.Length == 0 ? key : modifiers + "+" + key;
    }

    private static Dictionary<Key, string> BuildKeyNames()
    {
        // WPF 的 Key 列舉有別名（例如 PageUp 與 Prior 同值），因此一律使用明確名稱，不依賴 Enum.ToString。
        var names = new Dictionary<Key, string>();
        for (var i = 0; i < 26; i++)
        {
            names[Key.A + i] = ((char)('A' + i)).ToString();
        }

        for (var i = 0; i < 10; i++)
        {
            names[Key.D0 + i] = ((char)('0' + i)).ToString();
            names[Key.NumPad0 + i] = "Num" + i;
        }

        for (var i = 0; i < 24; i++)
        {
            names[Key.F1 + i] = "F" + (i + 1);
        }

        names[Key.Insert] = "Insert";
        names[Key.Delete] = "Delete";
        names[Key.Home] = "Home";
        names[Key.End] = "End";
        names[Key.PageUp] = "PageUp";
        names[Key.PageDown] = "PageDown";
        names[Key.Up] = "Up";
        names[Key.Down] = "Down";
        names[Key.Left] = "Left";
        names[Key.Right] = "Right";
        names[Key.Space] = "Space";
        names[Key.Pause] = "Pause";
        names[Key.Snapshot] = "PrintScreen";
        return names;
    }
}
