using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml.Linq;
using Microsoft.Win32;
using ScreenshotApp.Platform;
using ScreenshotApp.Resources;
using ScreenshotApp.Settings;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

[Collection(GdiObjectCountCollection.Name)]
public class LanguageTests
{
    [Fact]
    public void FreshAndLegacySettings_DefaultToEnglish()
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore(temp.Combine("settings.json"));
        Assert.Equal("en", new AppSettings().Language);
        Assert.Equal("en", store.Load().Settings.Language);
        File.WriteAllText(store.FilePath, """{"DefaultFontSize":36}""");
        var loaded = store.Load();
        Assert.Equal("en", loaded.Settings.Language);
        Assert.Equal(36, loaded.Settings.DefaultFontSize);
        Assert.Empty(loaded.InvalidFields);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("zh-TW")]
    public void Selection_RoundTrips(string language)
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore(temp.Combine("settings.json"));
        Assert.True(store.Save(new AppSettings { Language = language }).Succeeded);
        Assert.Equal(language, store.Load().Settings.Language);
        Assert.Equal(language, JsonNode.Parse(File.ReadAllText(store.FilePath))!["Language"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("false")]
    [InlineData("42")]
    [InlineData("\"\"")]
    [InlineData("\"fr\"")]
    [InlineData("\"zh-CN\"")]
    public void InvalidLanguage_ResetsOnlyLanguage(string json)
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("settings.json");
        File.WriteAllText(path, "{\"Language\":" + json + ",\"HistoryLimit\":77}");
        var loaded = new SettingsStore(path).Load();
        Assert.Equal("en", loaded.Settings.Language);
        Assert.Equal(77, loaded.Settings.HistoryLimit);
        Assert.Equal(new[] { SettingsStore.LanguageField }, loaded.InvalidFields);
    }

    [Theory]
    [InlineData("en", "Exit (&X)", "ScreenshotApp settings")]
    [InlineData("zh-TW", "結束(&X)", "ScreenshotApp 設定")]
    public void StartupCulture_ReachesNewThreadsAndXaml(string language, string exitText, string title) => StaRunner.Run(() =>
    {
        var previous = CultureInfo.CurrentUICulture;
        var previousDefault = CultureInfo.DefaultThreadCurrentUICulture;
        var numberCulture = CultureInfo.CurrentCulture;
        try
        {
            Strings.InitializeLanguage(language);
            Assert.Equal(language, CultureInfo.CurrentUICulture.Name);
            Assert.Equal(language, CultureInfo.DefaultThreadCurrentUICulture!.Name);
            Assert.Same(numberCulture, CultureInfo.CurrentCulture);
            Assert.Equal(exitText, Strings.Get("Tray_Exit"));
            string? onThread = null;
            var thread = new Thread(() => onThread = Strings.Get("Tray_Exit"));
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
            Assert.Equal(exitText, onThread);

            using var temp = new TempDirectory();
            var store = new SettingsStore(temp.Combine("settings.json"));
            using var controller = new AppController(store,
                new StartupService(Registry.CurrentUser, @"Software\ScreenshotApp.Tests\" + Guid.NewGuid().ToString("N"), "startup", "test.exe"),
                new ErrorLog(temp.Combine("logs")));
            Assert.True(controller.ApplySettings(controller.Settings with
            {
                Hotkey = new HotkeyGesture(System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt
                    | System.Windows.Input.ModifierKeys.Shift, System.Windows.Input.Key.F22),
                HistoryEnabled = false,
            }).Succeeded);
            var window = new SettingsWindow(controller);
            try
            {
                Assert.Equal(title, window.Title);
                Assert.Equal("en", window.LanguageBox.SelectedValue);
                Assert.Equal(2, window.LanguageBox.Items.Count);
                window.LanguageBox.SelectedValue = language;
                window.ApplyButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                Assert.Equal(language, controller.Settings.Language);
                Assert.Equal(language, store.Load().Settings.Language);
                Assert.Equal(title, window.Title);
                Assert.Equal(exitText, Strings.Get("Tray_Exit"));
            }
            finally { window.Close(); }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefault;
        }
    });

    [Fact]
    public void BothResourceFiles_HaveCompleteValuesAndMatchingFormatArguments()
    {
        var root = Path.Combine(RepositoryPaths.AppProject, "Resources");
        var english = Values(Path.Combine(root, "Strings.resx"));
        var chinese = Values(Path.Combine(root, "Strings.zh-TW.resx"));
        Assert.Equal(english.Keys.Order(), chinese.Keys.Order());
        var pattern = new Regex(@"\{\d+(?:[^{}]*)\}");
        foreach (var (key, value) in english)
        {
            Assert.False(string.IsNullOrWhiteSpace(value), key);
            Assert.False(string.IsNullOrWhiteSpace(chinese[key]), key);
            Assert.Equal(pattern.Matches(value).Select(x => x.Value).Order(), pattern.Matches(chinese[key]).Select(x => x.Value).Order());
            Assert.DoesNotContain(value, c => c is >= '\u4e00' and <= '\u9fff' && key != "Language_TraditionalChinese");
        }
    }

    private static Dictionary<string, string> Values(string path) => XDocument.Load(path).Root!.Elements("data")
        .ToDictionary(x => (string)x.Attribute("name")!, x => x.Element("value")!.Value);
}
