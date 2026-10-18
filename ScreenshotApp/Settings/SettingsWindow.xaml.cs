using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenshotApp.Platform;
using ScreenshotApp.Resources;

namespace ScreenshotApp.Settings;

internal partial class SettingsWindow : Window
{
    private static readonly Brush ErrorBrush = CreateFrozenBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));

    private readonly AppController _controller;
    private readonly ObservableCollection<NamedColor> _colors = [];
    private HotkeyGesture _pendingHotkey;

    public SettingsWindow(AppController controller)
    {
        _controller = controller;
        InitializeComponent();
        ColorList.ItemsSource = _colors;
        ShowSettings(controller.Settings);
    }

    private void ShowSettings(AppSettings settings)
    {
        _pendingHotkey = settings.Hotkey;
        HotkeyBox.Text = settings.Hotkey.ToString();
        ShowHotkeyStatus();

        StartWithWindowsBox.IsChecked = settings.StartWithWindows;
        LastSaveDirectoryBox.Text = settings.LastSaveDirectory ?? Strings.Get("Settings_LastSaveDirectoryNone");

        ShowColors(settings.DefaultColor);
        StrokeWidthBox.ItemsSource = StyleChoices.IncludeValue(StyleChoices.StrokeWidths, settings.DefaultStrokeWidth);
        StrokeWidthBox.SelectedItem = settings.DefaultStrokeWidth;
        FontSizeBox.ItemsSource = StyleChoices.IncludeValue(StyleChoices.FontSizes, settings.DefaultFontSize);
        FontSizeBox.SelectedItem = settings.DefaultFontSize;
    }

    private void ShowHotkeyStatus()
    {
        if (_controller.IsHotkeyActive)
        {
            HotkeyStatusText.Text = Strings.Format("Settings_HotkeyActive", _controller.Settings.Hotkey);
            HotkeyStatusText.ClearValue(TextBlock.ForegroundProperty);
        }
        else
        {
            HotkeyStatusText.Text = Strings.Format("Settings_HotkeyInactive", _controller.Settings.Hotkey, _controller.HotkeyFailureReason);
            HotkeyStatusText.Foreground = ErrorBrush;
        }
    }

    private void ShowColors(Color selected)
    {
        _colors.Clear();
        foreach (var color in CommonColors.All)
        {
            _colors.Add(color);
        }

        var match = _colors.FirstOrDefault(color => color.Color == selected);
        if (match is null)
        {
            match = CommonColors.Custom(selected);
            _colors.Add(match);
        }

        ColorList.SelectedItem = match;
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };
        var modifiers = Keyboard.Modifiers;

        // 沒有 Ctrl／Alt／Win 時保留 Tab、Enter、Esc 的原本用途，維持鍵盤導覽與確定／取消。
        if ((modifiers & ~ModifierKeys.Shift) == ModifierKeys.None && key is Key.Tab or Key.Enter or Key.Escape)
        {
            return;
        }

        e.Handled = true;
        if (IsModifierKey(key))
        {
            var partial = HotkeyGesture.FormatModifiers(modifiers);
            HotkeyBox.Text = partial.Length == 0 ? _pendingHotkey.ToString() : Strings.Format("Settings_HotkeyPartial", partial);
            return;
        }

        var gesture = new HotkeyGesture(modifiers, key);
        var validation = gesture.Validate();
        if (validation == HotkeyValidation.Valid)
        {
            _pendingHotkey = gesture;
            HotkeyBox.Text = gesture.ToString();
            ShowHotkeyStatus();
        }
        else
        {
            HotkeyBox.Text = _pendingHotkey.ToString();
            HotkeyStatusText.Text = Strings.Format("Settings_HotkeyInvalid", gesture, DescribeValidation(validation));
            HotkeyStatusText.Foreground = ErrorBrush;
        }
    }

    private void HotkeyBox_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        // 只按修飾鍵後放開時，恢復顯示目前選定的組合。
        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            HotkeyBox.Text = _pendingHotkey.ToString();
        }
    }

    private void HotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        HotkeyBox.Text = _pendingHotkey.ToString();

    private void CustomColorButton_Click(object sender, RoutedEventArgs e)
    {
        var current = (ColorList.SelectedItem as NamedColor)?.Color ?? _controller.Settings.DefaultColor;
        if (ColorPicker.TryPick(this, current, out var picked))
        {
            ShowColors(picked);
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (ApplyChanges())
        {
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ApplyButton_Click(object sender, RoutedEventArgs e) => ApplyChanges();

    private bool ApplyChanges()
    {
        var current = _controller.Settings;
        var proposed = current with
        {
            Hotkey = _pendingHotkey,
            StartWithWindows = StartWithWindowsBox.IsChecked == true,
            DefaultColor = (ColorList.SelectedItem as NamedColor)?.Color ?? current.DefaultColor,
            DefaultStrokeWidth = StrokeWidthBox.SelectedItem as int? ?? current.DefaultStrokeWidth,
            DefaultFontSize = FontSizeBox.SelectedItem as int? ?? current.DefaultFontSize,
        };

        var result = _controller.ApplySettings(proposed);
        ShowSettings(_controller.Settings);

        ResultText.Text = string.Join(Environment.NewLine, result.Messages);
        ResultText.Visibility = Visibility.Visible;
        if (result.Succeeded)
        {
            ResultText.ClearValue(TextBlock.ForegroundProperty);
        }
        else
        {
            ResultText.Foreground = ErrorBrush;
        }

        return result.Succeeded;
    }

    private static string DescribeValidation(HotkeyValidation validation) => validation switch
    {
        HotkeyValidation.MissingModifier => Strings.Get("Hotkey_MissingModifier"),
        HotkeyValidation.ReservedBySystem => Strings.Get("Hotkey_ReservedBySystem"),
        _ => Strings.Get("Hotkey_UnsupportedKey"),
    };

    private static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
