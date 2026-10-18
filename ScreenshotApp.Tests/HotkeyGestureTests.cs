using System.Windows.Input;
using ScreenshotApp.Settings;

namespace ScreenshotApp.Tests;

public class HotkeyGestureTests
{
    [Fact]
    public void Default_IsCtrlShift2()
    {
        Assert.Equal(new HotkeyGesture(ModifierKeys.Control | ModifierKeys.Shift, Key.D2), HotkeyGesture.Default);
        Assert.Equal("Ctrl+Shift+2", HotkeyGesture.Default.ToString());
    }

    [Theory]
    [InlineData("Ctrl+Shift+2", ModifierKeys.Control | ModifierKeys.Shift, Key.D2)]
    [InlineData("ctrl + alt + a", ModifierKeys.Control | ModifierKeys.Alt, Key.A)]
    [InlineData("Win+Shift+F24", ModifierKeys.Windows | ModifierKeys.Shift, Key.F24)]
    [InlineData("Control+Num7", ModifierKeys.Control, Key.NumPad7)]
    [InlineData("Alt+PageUp", ModifierKeys.Alt, Key.PageUp)]
    [InlineData("Windows+Space", ModifierKeys.Windows, Key.Space)]
    public void TryParse_ValidText_ReturnsGesture(string text, ModifierKeys modifiers, Key key)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var gesture));
        Assert.Equal(new HotkeyGesture(modifiers, key), gesture);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Hyper+A")]
    [InlineData("Ctrl+Escape")]
    [InlineData("Ctrl+A+B")]
    public void TryParse_InvalidText_ReturnsFalse(string? text)
    {
        Assert.False(HotkeyGesture.TryParse(text, out _));
    }

    [Theory]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift, Key.D2, "Ctrl+Shift+2")]
    [InlineData(ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Windows, Key.F1, "Win+Ctrl+Alt+Shift+F1")]
    [InlineData(ModifierKeys.Alt, Key.PageDown, "Alt+PageDown")]
    [InlineData(ModifierKeys.Control, Key.NumPad0, "Ctrl+Num0")]
    [InlineData(ModifierKeys.Windows | ModifierKeys.Shift, Key.Snapshot, "Win+Shift+PrintScreen")]
    public void ToString_UsesFixedModifierOrderAndExplicitKeyNames(ModifierKeys modifiers, Key key, string expected)
    {
        Assert.Equal(expected, new HotkeyGesture(modifiers, key).ToString());
    }

    [Fact]
    public void ToString_RoundTripsThroughTryParse_ForEverySupportedKey()
    {
        var supportedKeys = Enum.GetValues<Key>()
            .Distinct()
            .Select(key => new HotkeyGesture(ModifierKeys.Control | ModifierKeys.Alt, key))
            .Where(gesture => gesture.Validate() != HotkeyValidation.UnsupportedKey)
            .ToList();

        Assert.True(supportedKeys.Count >= 70);
        foreach (var gesture in supportedKeys)
        {
            Assert.True(HotkeyGesture.TryParse(gesture.ToString(), out var parsed), gesture.ToString());
            Assert.Equal(gesture, parsed);
        }
    }

    [Theory]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift, Key.D2, nameof(HotkeyValidation.Valid))]
    [InlineData(ModifierKeys.Alt, Key.F5, nameof(HotkeyValidation.Valid))]
    [InlineData(ModifierKeys.Windows, Key.Q, nameof(HotkeyValidation.Valid))]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift, Key.S, nameof(HotkeyValidation.Valid))]
    [InlineData(ModifierKeys.None, Key.A, nameof(HotkeyValidation.MissingModifier))]
    [InlineData(ModifierKeys.Shift, Key.F5, nameof(HotkeyValidation.MissingModifier))]
    [InlineData(ModifierKeys.None, Key.Snapshot, nameof(HotkeyValidation.ReservedBySystem))]
    [InlineData(ModifierKeys.Control, Key.Snapshot, nameof(HotkeyValidation.ReservedBySystem))]
    [InlineData(ModifierKeys.Windows | ModifierKeys.Shift, Key.S, nameof(HotkeyValidation.ReservedBySystem))]
    [InlineData(ModifierKeys.Control, Key.Escape, nameof(HotkeyValidation.UnsupportedKey))]
    [InlineData(ModifierKeys.Control, Key.LeftShift, nameof(HotkeyValidation.UnsupportedKey))]
    public void Validate_ClassifiesGestures(ModifierKeys modifiers, Key key, string expected)
    {
        Assert.Equal(Enum.Parse<HotkeyValidation>(expected), new HotkeyGesture(modifiers, key).Validate());
    }

    [Fact]
    public void VirtualKey_MatchesWin32KeyCodes()
    {
        Assert.Equal(0x32, HotkeyGesture.Default.VirtualKey);
        Assert.Equal(0x87, new HotkeyGesture(ModifierKeys.Control, Key.F24).VirtualKey);
    }

    [Fact]
    public void ModifierFlags_MatchRegisterHotKeyModifierFlags()
    {
        Assert.Equal(0x1, (int)ModifierKeys.Alt);
        Assert.Equal(0x2, (int)ModifierKeys.Control);
        Assert.Equal(0x4, (int)ModifierKeys.Shift);
        Assert.Equal(0x8, (int)ModifierKeys.Windows);
    }
}
