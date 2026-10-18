using System.IO;
using System.Security;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media;
using ScreenshotApp.Resources;

namespace ScreenshotApp.Settings;

internal enum SettingsLoadStatus
{
    /// <summary>設定檔已讀取；個別無效欄位列於結果的無效欄位清單。</summary>
    Loaded,

    /// <summary>設定檔不存在，使用預設值。</summary>
    Missing,

    /// <summary>設定檔內容無法解析，使用預設值；原檔已另存備份。</summary>
    Corrupt,

    /// <summary>無法讀取設定檔，使用預設值。</summary>
    ReadFailed,
}

internal sealed record SettingsLoadResult(
    AppSettings Settings,
    SettingsLoadStatus Status,
    IReadOnlyList<string> InvalidFields,
    string? ErrorMessage);

internal sealed record SettingsSaveResult(bool Succeeded, string? ErrorMessage);

/// <summary>
/// 以 JSON 檔保存設定。讀取時任何異常都回復為預設值並回報，不拋出例外；
/// 個別欄位無效只重設該欄位。寫入先寫暫存檔再取代正式檔，避免中途失敗留下半截檔案。
/// </summary>
internal sealed class SettingsStore
{
    public const string HotkeyField = "Hotkey";
    public const string StartWithWindowsField = "StartWithWindows";
    public const string LastSaveDirectoryField = "LastSaveDirectory";
    public const string DefaultColorField = "DefaultColor";
    public const string DefaultStrokeWidthField = "DefaultStrokeWidth";
    public const string DefaultFontSizeField = "DefaultFontSize";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        // 保留中文路徑的可讀性；此檔案不會被嵌入網頁，不需要 HTML 跳脫。
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private delegate bool FieldReader<T>(JsonNode? node, out T value);

    public SettingsStore(string filePath)
    {
        FilePath = filePath;
    }

    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenshotApp", "settings.json");

    public string FilePath { get; }

    public string CorruptBackupPath =>
        Path.Combine(Path.GetDirectoryName(FilePath) ?? string.Empty, Path.GetFileNameWithoutExtension(FilePath) + ".corrupt.json");

    public static string DescribeField(string field) => field switch
    {
        HotkeyField => Strings.Get("SettingsField_Hotkey"),
        StartWithWindowsField => Strings.Get("SettingsField_StartWithWindows"),
        LastSaveDirectoryField => Strings.Get("SettingsField_LastSaveDirectory"),
        DefaultColorField => Strings.Get("SettingsField_DefaultColor"),
        DefaultStrokeWidthField => Strings.Get("SettingsField_DefaultStrokeWidth"),
        DefaultFontSizeField => Strings.Get("SettingsField_DefaultFontSize"),
        _ => field,
    };

    public SettingsLoadResult Load()
    {
        string json;
        try
        {
            if (!File.Exists(FilePath))
            {
                return new SettingsLoadResult(new AppSettings(), SettingsLoadStatus.Missing, [], null);
            }

            json = File.ReadAllText(FilePath, Encoding.UTF8);
        }
        catch (Exception ex) when (IsFileSystemFailure(ex))
        {
            return new SettingsLoadResult(new AppSettings(), SettingsLoadStatus.ReadFailed, [], ex.Message);
        }

        try
        {
            if (JsonNode.Parse(json) is JsonObject root)
            {
                var invalidFields = new List<string>();
                return new SettingsLoadResult(ReadSettings(root, invalidFields), SettingsLoadStatus.Loaded, invalidFields, null);
            }
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            // 視為損毀，於下方統一處理。
        }

        BackupCorruptFile();
        return new SettingsLoadResult(new AppSettings(), SettingsLoadStatus.Corrupt, [], null);
    }

    public SettingsSaveResult Save(AppSettings settings)
    {
        var root = new JsonObject
        {
            [HotkeyField] = settings.Hotkey.ToString(),
            [StartWithWindowsField] = settings.StartWithWindows,
            [LastSaveDirectoryField] = settings.LastSaveDirectory,
            [DefaultColorField] = ColorText.Format(settings.DefaultColor),
            [DefaultStrokeWidthField] = settings.DefaultStrokeWidth,
            [DefaultFontSizeField] = settings.DefaultFontSize,
        };

        var tempPath = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(tempPath, root.ToJsonString(WriteOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, FilePath, overwrite: true);
            return new SettingsSaveResult(true, null);
        }
        catch (Exception ex) when (IsFileSystemFailure(ex))
        {
            TryDelete(tempPath);
            return new SettingsSaveResult(false, ex.Message);
        }
    }

    private static AppSettings ReadSettings(JsonObject root, List<string> invalidFields)
    {
        var defaults = new AppSettings();
        return new AppSettings
        {
            Hotkey = ReadField<HotkeyGesture>(root, HotkeyField, defaults.Hotkey, invalidFields, TryReadHotkey),
            StartWithWindows = ReadField<bool>(root, StartWithWindowsField, defaults.StartWithWindows, invalidFields, TryReadBoolean),
            LastSaveDirectory = ReadField<string?>(root, LastSaveDirectoryField, defaults.LastSaveDirectory, invalidFields, TryReadDirectory),
            DefaultColor = ReadField<Color>(root, DefaultColorField, defaults.DefaultColor, invalidFields, TryReadColor),
            DefaultStrokeWidth = ReadField(root, DefaultStrokeWidthField, defaults.DefaultStrokeWidth, invalidFields,
                (JsonNode? node, out int value) => TryReadInt(node, AppSettings.MinStrokeWidth, AppSettings.MaxStrokeWidth, out value)),
            DefaultFontSize = ReadField(root, DefaultFontSizeField, defaults.DefaultFontSize, invalidFields,
                (JsonNode? node, out int value) => TryReadInt(node, AppSettings.MinFontSize, AppSettings.MaxFontSize, out value)),
        };
    }

    private static T ReadField<T>(JsonObject root, string field, T fallback, List<string> invalidFields, FieldReader<T> reader)
    {
        // 缺少欄位沿用預設值且不視為錯誤，讓較舊的設定檔可以直接使用。
        if (!root.TryGetPropertyValue(field, out var node))
        {
            return fallback;
        }

        if (reader(node, out var value))
        {
            return value;
        }

        invalidFields.Add(field);
        return fallback;
    }

    private static bool TryReadHotkey(JsonNode? node, out HotkeyGesture value)
    {
        value = default;
        return TryReadString(node, out var text)
            && HotkeyGesture.TryParse(text, out value)
            && value.Validate() == HotkeyValidation.Valid;
    }

    private static bool TryReadBoolean(JsonNode? node, out bool value)
    {
        value = node?.GetValueKind() == JsonValueKind.True;
        return node?.GetValueKind() is JsonValueKind.True or JsonValueKind.False;
    }

    private static bool TryReadDirectory(JsonNode? node, out string? value)
    {
        value = null;
        if (node is null)
        {
            return true;
        }

        if (!TryReadString(node, out var text))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        // 只檢查格式；資料夾是否仍存在由使用端在使用時判斷。
        if (!Path.IsPathFullyQualified(text) || text.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return false;
        }

        value = text;
        return true;
    }

    private static bool TryReadColor(JsonNode? node, out Color value)
    {
        value = default;
        return TryReadString(node, out var text) && ColorText.TryParse(text, out value);
    }

    private static bool TryReadInt(JsonNode? node, int min, int max, out int value)
    {
        value = 0;
        return node is JsonValue jsonValue
            && jsonValue.GetValueKind() == JsonValueKind.Number
            && jsonValue.TryGetValue(out value)
            && value >= min
            && value <= max;
    }

    private static bool TryReadString(JsonNode? node, out string text)
    {
        text = string.Empty;
        return node is JsonValue jsonValue
            && jsonValue.GetValueKind() == JsonValueKind.String
            && jsonValue.TryGetValue(out text!);
    }

    private void BackupCorruptFile()
    {
        try
        {
            File.Copy(FilePath, CorruptBackupPath, overwrite: true);
        }
        catch (Exception ex) when (IsFileSystemFailure(ex))
        {
            // 備份失敗不影響回復預設值。
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (IsFileSystemFailure(ex))
        {
            // 暫存檔殘留不影響設定內容。
        }
    }

    private static bool IsFileSystemFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException;
}
