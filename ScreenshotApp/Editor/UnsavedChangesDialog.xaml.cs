using System.Windows;

namespace ScreenshotApp.Editor;

internal enum UnsavedChangesChoice
{
    Copy,
    Save,
    Discard,
    Cancel,
}

/// <summary>文件有未匯出的變更時，詢問使用者要複製、另存、捨棄或取消。按 Esc 或關閉對話框等同取消。</summary>
internal sealed partial class UnsavedChangesDialog : Window
{
    private UnsavedChangesChoice _choice = UnsavedChangesChoice.Cancel;

    private UnsavedChangesDialog()
    {
        InitializeComponent();
    }

    public static UnsavedChangesChoice Ask(Window owner)
    {
        var dialog = new UnsavedChangesDialog { Owner = owner };
        dialog.ShowDialog();
        return dialog._choice;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Choose(UnsavedChangesChoice.Save);

    private void Copy_Click(object sender, RoutedEventArgs e) => Choose(UnsavedChangesChoice.Copy);

    private void Discard_Click(object sender, RoutedEventArgs e) => Choose(UnsavedChangesChoice.Discard);

    private void Choose(UnsavedChangesChoice choice)
    {
        _choice = choice;
        DialogResult = true;
    }
}
