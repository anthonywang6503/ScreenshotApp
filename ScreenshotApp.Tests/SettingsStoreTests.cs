using System.Text.Json.Nodes;
using System.Windows.Input;
using System.Windows.Media;
using ScreenshotApp.Resources;
using ScreenshotApp.Settings;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

/// <summary>設定持久化：正常載入、快捷鍵變更、損毀復原、無效欄位、權限不足與無效路徑。</summary>
public sealed class SettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var store = CreateStore();

        var result = store.Load();

        Assert.Equal(SettingsLoadStatus.Missing, result.Status);
        Assert.Equal(new AppSettings(), result.Settings);
        Assert.Empty(result.InvalidFields);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var store = CreateStore();
        var settings = new AppSettings
        {
            Hotkey = new HotkeyGesture(ModifierKeys.Control | ModifierKeys.Alt, Key.F9),
            StartWithWindows = true,
            LastSaveDirectory = @"D:\圖片\截圖 資料夾",
            DefaultColor = Color.FromRgb(0x12, 0xAB, 0xEF),
            DefaultStrokeWidth = 7,
            DefaultFontSize = 36,
        };

        Assert.True(store.Save(settings).Succeeded);
        var result = store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Empty(result.InvalidFields);
        Assert.Equal(settings, result.Settings);
    }

    [Fact]
    public void Load_HotkeyEditedInFile_UsesTheNewHotkey()
    {
        var store = CreateStore();
        File.WriteAllText(store.FilePath, """{ "Hotkey": "Ctrl+Alt+A" }""");

        var result = store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Equal(new HotkeyGesture(ModifierKeys.Control | ModifierKeys.Alt, Key.A), result.Settings.Hotkey);
    }

    [Fact]
    public void Load_EmptyObject_UsesDefaultsWithoutWarnings()
    {
        var store = CreateStore();
        File.WriteAllText(store.FilePath, "{}");

        var result = store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Empty(result.InvalidFields);
        Assert.Equal(new AppSettings(), result.Settings);
    }

    [Theory]
    [InlineData("{ \"Hotkey\": ")]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"Hotkey\": \"Ctrl+Shift+2\", \"Hotkey\": \"Ctrl+Alt+A\" }")]
    public void Load_CorruptContent_ReturnsDefaultsAndBacksUpTheOriginal(string content)
    {
        var store = CreateStore();
        File.WriteAllText(store.FilePath, content);

        var result = store.Load();

        Assert.Equal(SettingsLoadStatus.Corrupt, result.Status);
        Assert.Equal(new AppSettings(), result.Settings);
        Assert.Equal(content, File.ReadAllText(store.CorruptBackupPath));
    }

    [Fact]
    public void Load_InvalidFields_ResetsOnlyThoseFields()
    {
        var store = CreateStore();
        File.WriteAllText(store.FilePath, """
            {
              "Hotkey": "Shift+A",
              "StartWithWindows": "yes",
              "LastSaveDirectory": "relative\\folder",
              "DefaultColor": "red",
              "DefaultStrokeWidth": 0,
              "DefaultFontSize": 24
            }
            """);

        var result = store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Equal(
            [
                SettingsStore.HotkeyField,
                SettingsStore.StartWithWindowsField,
                SettingsStore.LastSaveDirectoryField,
                SettingsStore.DefaultColorField,
                SettingsStore.DefaultStrokeWidthField,
            ],
            result.InvalidFields);
        var defaults = new AppSettings();
        Assert.Equal(defaults with { DefaultFontSize = 24 }, result.Settings);
    }

    [Theory]
    [InlineData("\"Hotkey\": \"Win+Shift+S\"", SettingsStore.HotkeyField)]
    [InlineData("\"Hotkey\": \"Ctrl+PrintScreen\"", SettingsStore.HotkeyField)]
    [InlineData("\"Hotkey\": 42", SettingsStore.HotkeyField)]
    [InlineData("\"StartWithWindows\": null", SettingsStore.StartWithWindowsField)]
    [InlineData("\"DefaultColor\": \"#12345\"", SettingsStore.DefaultColorField)]
    [InlineData("\"DefaultStrokeWidth\": 51", SettingsStore.DefaultStrokeWidthField)]
    [InlineData("\"DefaultStrokeWidth\": 2.5", SettingsStore.DefaultStrokeWidthField)]
    [InlineData("\"DefaultFontSize\": \"20\"", SettingsStore.DefaultFontSizeField)]
    [InlineData("\"DefaultFontSize\": 7", SettingsStore.DefaultFontSizeField)]
    [InlineData("\"LastSaveDirectory\": 5", SettingsStore.LastSaveDirectoryField)]
    [InlineData("\"LastSaveDirectory\": \"C:\\\\bad|name\"", SettingsStore.LastSaveDirectoryField)]
    public void Load_SingleInvalidField_IsReported(string field, string expectedInvalidField)
    {
        var store = CreateStore();
        File.WriteAllText(store.FilePath, "{ " + field + " }");

        var result = store.Load();

        Assert.Equal([expectedInvalidField], result.InvalidFields);
        Assert.Equal(new AppSettings(), result.Settings);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public void Load_EmptyLastSaveDirectory_IsAcceptedAsNone(string value)
    {
        var store = CreateStore();
        File.WriteAllText(store.FilePath, "{ \"LastSaveDirectory\": " + value + " }");

        var result = store.Load();

        Assert.Empty(result.InvalidFields);
        Assert.Null(result.Settings.LastSaveDirectory);
    }

    [Fact]
    public void Load_FileCannotBeOpened_ReturnsDefaultsWithReason()
    {
        var store = CreateStore();
        Assert.True(store.Save(new AppSettings { DefaultFontSize = 48 }).Succeeded);

        using (new FileStream(store.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = store.Load();

            Assert.Equal(SettingsLoadStatus.ReadFailed, result.Status);
            Assert.Equal(new AppSettings(), result.Settings);
            Assert.Equal(Strings.Get("Error_FileInUse"), result.ErrorMessage);
        }
    }

    [Fact]
    public void Save_ReadOnlyTarget_FailsAndKeepsTheOriginalFile()
    {
        var store = CreateStore();
        Assert.True(store.Save(new AppSettings { DefaultFontSize = 48 }).Succeeded);
        var original = File.ReadAllText(store.FilePath);
        File.SetAttributes(store.FilePath, FileAttributes.ReadOnly);

        var result = store.Save(new AppSettings { DefaultFontSize = 64 });

        Assert.False(result.Succeeded);
        Assert.Equal(Strings.Get("Error_AccessDenied"), result.ErrorMessage);
        Assert.Equal(original, File.ReadAllText(store.FilePath));
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact]
    public void SaveAndLoad_InvalidPath_DoNotThrow()
    {
        var store = new SettingsStore(_temp.Combine("bad|folder", "settings.json"));

        var save = store.Save(new AppSettings());
        var load = store.Load();

        Assert.False(save.Succeeded);
        Assert.Equal(new AppSettings(), load.Settings);
    }

    [Fact]
    public void Save_CreatesDirectoryAndLeavesNoTemporaryFile()
    {
        var store = new SettingsStore(_temp.Combine("nested", "ScreenshotApp", "settings.json"));

        Assert.True(store.Save(new AppSettings()).Succeeded);

        var files = Directory.GetFiles(Path.GetDirectoryName(store.FilePath)!);
        Assert.Equal([store.FilePath], files);
    }

    [Fact]
    public void Save_WritesReadableJsonWithStableFieldNames()
    {
        var store = CreateStore();

        Assert.True(store.Save(new AppSettings()).Succeeded);

        var root = JsonNode.Parse(File.ReadAllText(store.FilePath))!.AsObject();
        Assert.Equal("Ctrl+Shift+2", (string?)root[SettingsStore.HotkeyField]);
        Assert.False((bool)root[SettingsStore.StartWithWindowsField]!);
        Assert.Null(root[SettingsStore.LastSaveDirectoryField]);
        Assert.Equal("#FF0000", (string?)root[SettingsStore.DefaultColorField]);
        Assert.Equal(3, (int)root[SettingsStore.DefaultStrokeWidthField]!);
        Assert.Equal(20, (int)root[SettingsStore.DefaultFontSizeField]!);
    }

    [Fact]
    public void DefaultFilePath_IsUnderLocalAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.Equal(Path.Combine(localAppData, "ScreenshotApp", "settings.json"), SettingsStore.DefaultFilePath);
    }

    private SettingsStore CreateStore() => new(_temp.Combine("settings.json"));
}
