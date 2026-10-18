using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ScreenshotApp.Platform;

/// <summary>以 Windows 色彩對話框選擇自訂顏色（WPF 沒有內建的色彩對話框）。</summary>
internal static class ColorPicker
{
    public static bool TryPick(Window owner, Color initial, out Color picked)
    {
        using var dialog = new Forms.ColorDialog
        {
            AnyColor = true,
            FullOpen = true,
            Color = Drawing.Color.FromArgb(initial.R, initial.G, initial.B),
        };

        var result = dialog.ShowDialog(new OwnerWindow(new WindowInteropHelper(owner).Handle));
        picked = Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
        return result == Forms.DialogResult.OK;
    }

    private sealed class OwnerWindow(nint handle) : Forms.IWin32Window
    {
        public nint Handle { get; } = handle;
    }
}
