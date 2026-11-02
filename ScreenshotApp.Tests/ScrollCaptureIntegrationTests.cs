using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ScreenshotApp.Capture;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.History;
using ScreenshotApp.Platform;
using ScreenshotApp.Settings;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

public class ScrollCaptureIntegrationTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private static readonly ModifierKeys Modifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift;
    private static readonly HotkeyGesture Regular = new(Modifiers, Key.F17);
    private static readonly HotkeyGesture Scroll = new(Modifiers, Key.F18);
    private static readonly HotkeyGesture Blocked = new(Modifiers, Key.F19);
    public void Dispose() => _temp.Dispose();

    private AppController Controller() => new(new SettingsStore(_temp.Combine("settings.json")),
        new StartupService(Registry.CurrentUser, @"Software\ScreenshotApp.Tests\" + Guid.NewGuid().ToString("N"), "startup", "test.exe"),
        new ErrorLog(_temp.Combine("logs")), new HistoryStore(_temp.Combine("history")));

    [Fact]
    public void AT08_OptionalScrollHotkey_RoundTripsAndCanBeCleared()
    {
        var store = new SettingsStore(_temp.Combine("settings.json"));
        Assert.Null(store.Load().Settings.ScrollHotkey);
        Assert.True(store.Save(new AppSettings { ScrollHotkey = Scroll }).Succeeded);
        Assert.Equal(Scroll, store.Load().Settings.ScrollHotkey);
        Assert.True(store.Save(new AppSettings()).Succeeded);
        Assert.Null(store.Load().Settings.ScrollHotkey);
        Assert.Empty(store.Load().InvalidFields);
    }

    [Theory]
    [InlineData("\"Ctrl+Shift+2\"")]
    [InlineData("\"A\"")]
    [InlineData("true")]
    [InlineData("42")]
    [InlineData("\"\"")]
    public void AT08_InvalidOrDuplicateScrollHotkey_ResetsOnlyOptionalField(string json)
    {
        var path = _temp.Combine("settings.json");
        File.WriteAllText(path, "{\"HistoryLimit\":77,\"ScrollHotkey\":" + json + "}");
        var loaded = new SettingsStore(path).Load();
        Assert.Null(loaded.Settings.ScrollHotkey);
        Assert.Equal(77, loaded.Settings.HistoryLimit);
        Assert.Equal(new[] { SettingsStore.ScrollHotkeyField }, loaded.InvalidFields);
    }

    [Fact]
    public void AT08_RegistrationConflictRetainsOldScrollHotkey_AndDuplicateIsRejected() => StaRunner.Run(() =>
    {
        using var controller = Controller();
        var proposed = controller.Settings with { Hotkey = Regular, ScrollHotkey = Scroll, HistoryEnabled = false };
        Assert.True(controller.ApplySettings(proposed).Succeeded);
        using var blocker = new HotkeyService();
        Assert.True(blocker.Register(Blocked).Succeeded);
        Assert.False(controller.ApplySettings(proposed with { ScrollHotkey = Blocked }).Succeeded);
        Assert.Equal(Scroll, controller.Settings.ScrollHotkey);
        Assert.False(controller.ApplySettings(proposed with { ScrollHotkey = Regular }).Succeeded);
        Assert.Equal(Scroll, controller.Settings.ScrollHotkey);
        Assert.True(controller.ApplySettings(proposed with { ScrollHotkey = null }).Succeeded);
        using var probe = new HotkeyService();
        Assert.True(probe.Register(Scroll).Succeeded);
        Assert.True(controller.IsHotkeyActive);
    });

    [Fact]
    public void AT16_BusyScrolling_BlocksAllEntrypointsWithoutShowingWindows() => StaRunner.Run(() =>
    {
        using var controller = Controller();
        var state = (AppStateMachine)typeof(AppController).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
        state.TryBeginCapture(); state.BeginSelecting();
        controller.OpenImage(); controller.ShowSettings(); controller.ShowHistory(); controller.ActivateFromSecondInstance();
        foreach (var name in new[] { "StartCapture", "StartScrollCapture", "RequestExit" })
            typeof(AppController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(controller, null);
        foreach (var name in new[] { "_editor", "_settingsWindow", "_historyWindow" })
            Assert.Null(typeof(AppController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller));
        Assert.Equal(AppState.Selecting, state.Current);
        state.EndCapture();
    });

    [Fact]
    public void AT16_PanelAndSettings_AreAvailableWithoutShowing() => StaRunner.Run(() =>
    {
        using var controller = Controller();
        var settings = new SettingsWindow(controller);
        Assert.Contains("未設定", settings.ScrollHotkeyBox.Text);
        var session = new ScrollCaptureSession(ScrollStitcherTests.Frame(0));
        var panel = new ScrollCaptureWindow(new PixelRect(0, 0, 7, 96), session);
        foreach (var name in new[] { "HeightText", "PreviewImage", "AutomaticButton", "FinishButton", "CancelCaptureButton", "StatusText" })
            Assert.NotNull(panel.FindName(name));
        Assert.Equal("7 × 96 像素", panel.HeightText.Text);
        Assert.NotNull(panel.PreviewImage.Source);
        panel.Close(); settings.Close();
    });

    [Fact]
    public void AT08_ClearOptionalHotkey_RefreshesPreviousValidationError() => StaRunner.Run(() =>
    {
        using var controller = Controller();
        var settings = new SettingsWindow(controller);
        settings.ScrollHotkeyStatusText.Text = "無效組合";
        settings.ScrollHotkeyStatusText.Foreground = Brushes.Red;
        typeof(SettingsWindow).GetMethod("ClearScrollHotkey_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(settings, [settings, new RoutedEventArgs()]);
        Assert.Equal(controller.ScrollHotkeyStatus, settings.ScrollHotkeyStatusText.Text);
        Assert.Equal(DependencyProperty.UnsetValue, settings.ScrollHotkeyStatusText.ReadLocalValue(System.Windows.Controls.TextBlock.ForegroundProperty));
        settings.Close();
    });

    [Fact]
    public void AT16_FrameEdgesNeverOverlapSample_EvenWithNegativeDesktopCoordinates()
    {
        var sample = new PixelRect(-1200, -300, 800, 600);
        foreach (var edge in ScrollCaptureFrame.Edges(sample)) Assert.True(edge.Intersect(sample).IsEmpty);
        var position = ScrollCaptureWindow.PanelPosition(sample, new PixelRect(-1920, -600, 1920, 1200), 310, 400);
        Assert.True(new PixelRect(position.X, position.Y, 310, 400).Intersect(sample).IsEmpty);
        Assert.Equal(new PixelPoint(-1920, -600), ScrollCaptureWindow.PanelPosition(new PixelRect(-1920, -600, 1920, 1200),
            new PixelRect(-1920, -600, 1920, 1200), 310, 400));
    }

    [Theory]
    [InlineData(101U, 2, 3, false, true)]
    [InlineData(99U, 2, 3, false, true)]
    [InlineData(100U, 3, 3, false, true)]
    [InlineData(100U, 2, 3, true, true)]
    [InlineData(100U, 2, 3, false, false)]
    public void AT16_UserInputStopsAutomatic_EvenWhenTimestampMovesBackward(uint time, int x, int y, bool key, bool interrupted)
    {
        var baseline = new ScrollInputStamp(100, new PixelPoint(2, 3), false);
        Assert.Equal(interrupted, new ScrollInputStamp(time, new PixelPoint(x, y), key).Interrupts(baseline));
    }
}
