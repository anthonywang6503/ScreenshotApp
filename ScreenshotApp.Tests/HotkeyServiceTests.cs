using System.Runtime.InteropServices;
using System.Windows.Input;
using ScreenshotApp.Platform;
using ScreenshotApp.Settings;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

/// <summary>
/// 以真實的 RegisterHotKey 驗證註冊、衝突與變更行為。只使用 Ctrl+Alt+Shift+F21～F24 這類幾乎不會被占用的組合，
/// 不使用預設的 Ctrl+Shift+2，以免與正在執行的應用程式互相干擾。
/// </summary>
public partial class HotkeyServiceTests
{
    private const ModifierKeys TestModifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift;

    private static readonly HotkeyGesture GestureA = new(TestModifiers, Key.F21);
    private static readonly HotkeyGesture GestureB = new(TestModifiers, Key.F22);

    [Fact]
    public void Register_TakesTheCombinationSystemWide()
    {
        StaRunner.Run(() =>
        {
            using var service = new HotkeyService();
            using var other = new HotkeyService();

            Assert.True(service.Register(GestureA).Succeeded);
            Assert.Equal(GestureA, service.Current);

            var conflict = other.Register(GestureA);
            Assert.False(conflict.Succeeded);
            Assert.Equal(NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED, conflict.ErrorCode);
            Assert.Null(other.Current);
        });
    }

    [Fact]
    public void Register_NewCombination_ReleasesThePreviousOne()
    {
        StaRunner.Run(() =>
        {
            using var service = new HotkeyService();
            using var probe = new HotkeyService();
            Assert.True(service.Register(GestureA).Succeeded);

            Assert.True(service.Register(GestureB).Succeeded);

            Assert.Equal(GestureB, service.Current);
            Assert.True(probe.Register(GestureA).Succeeded);
        });
    }

    [Fact]
    public void Register_WhenNewCombinationIsTaken_KeepsThePreviousRegistration()
    {
        StaRunner.Run(() =>
        {
            using var blocker = new HotkeyService();
            using var service = new HotkeyService();
            using var probe = new HotkeyService();
            Assert.True(blocker.Register(GestureB).Succeeded);
            Assert.True(service.Register(GestureA).Succeeded);

            var result = service.Register(GestureB);

            Assert.False(result.Succeeded);
            Assert.Equal(NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED, result.ErrorCode);
            Assert.Equal(GestureA, service.Current);
            Assert.False(probe.Register(GestureA).Succeeded);
        });
    }

    [Fact]
    public void Register_SameCombinationAgain_Succeeds()
    {
        StaRunner.Run(() =>
        {
            using var service = new HotkeyService();
            Assert.True(service.Register(GestureA).Succeeded);

            Assert.True(service.Register(GestureA).Succeeded);
            Assert.Equal(GestureA, service.Current);
        });
    }

    [Fact]
    public void Register_InvalidGesture_Throws()
    {
        StaRunner.Run(() =>
        {
            using var service = new HotkeyService();

            Assert.Throws<ArgumentException>(() => service.Register(new HotkeyGesture(ModifierKeys.None, Key.A)));
            Assert.Throws<ArgumentException>(() => service.Register(new HotkeyGesture(ModifierKeys.Windows | ModifierKeys.Shift, Key.S)));
        });
    }

    [Fact]
    public void Dispose_ReleasesTheCombination()
    {
        StaRunner.Run(() =>
        {
            var service = new HotkeyService();
            Assert.True(service.Register(GestureA).Succeeded);

            service.Dispose();

            using var probe = new HotkeyService();
            Assert.True(probe.Register(GestureA).Succeeded);
        });
    }

    [Fact]
    public void HotkeyMessage_RaisesPressedOnlyForTheCurrentRegistration()
    {
        StaRunner.Run(() =>
        {
            using var service = new HotkeyService();
            var pressedCount = 0;
            service.Pressed += (_, _) => pressedCount++;
            Assert.True(service.Register(GestureA).Succeeded);

            PostMessage(service.WindowHandle, NativeMethods.WM_HOTKEY, service.RegisteredId, 0);
            PostMessage(service.WindowHandle, NativeMethods.WM_HOTKEY, 99, 0);
            StaRunner.DoEvents();

            Assert.Equal(1, pressedCount);
        });
    }

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);
}
