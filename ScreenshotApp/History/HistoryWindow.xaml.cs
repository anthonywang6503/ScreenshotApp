using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenshotApp.Resources;

namespace ScreenshotApp.History;

internal partial class HistoryWindow : Window
{
    private readonly AppController _controller;
    private bool _busy;
    private int _refreshVersion;

    public HistoryWindow(AppController controller)
    {
        _controller = controller;
        InitializeComponent();
        UpdateButtons();
        Loaded += async (_, _) => await RefreshAsync();
    }

    internal async Task RefreshAsync()
    {
        var version = ++_refreshVersion;
        var selected = (EntriesList.SelectedItem as HistoryEntry)?.Id;
        var result = await _controller.ListHistoryAsync();
        if (version != _refreshVersion) return;
        EntriesList.ItemsSource = result.Value;
        EntriesList.SelectedItem = result.Value?.FirstOrDefault(entry => entry.Id == selected);
        if (EntriesList.SelectedIndex < 0 && EntriesList.Items.Count > 0) EntriesList.SelectedIndex = 0;
        StatusText.Text = result.Error ?? (EntriesList.Items.Count == 0 ? Strings.Get("History_Empty") : string.Empty);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var selected = EntriesList.SelectedItem is HistoryEntry && !_busy;
        OpenHistoryButton.IsEnabled = CopyHistoryButton.IsEnabled = DeleteHistoryButton.IsEnabled = selected;
    }

    private void Selection_Changed(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private async Task RunAsync(Func<HistoryEntry?, Task<string?>> action, bool refresh = false)
    {
        if (_busy || !_controller.CanUseHistory) return;
        _busy = true;
        HistoryContent.IsEnabled = false;
        try
        {
            var message = await action(EntriesList.SelectedItem as HistoryEntry);
            if (refresh) await RefreshAsync();
            if (message is not null) StatusText.Text = message;
        }
        finally
        {
            _busy = false;
            HistoryContent.IsEnabled = true;
            UpdateButtons();
        }
    }

    private async void Open_Click(object sender, RoutedEventArgs e) => await RunAsync(async entry =>
    {
        if (entry is not null) await _controller.OpenHistoryAsync(entry.Id);
        return null;
    });

    private async void Copy_Click(object sender, RoutedEventArgs e) => await RunAsync(async entry =>
        entry is null ? null : await _controller.CopyHistoryAsync(entry.Id) ?? Strings.Get("History_Copied"));

    private async void Delete_Click(object sender, RoutedEventArgs e) => await RunAsync(async entry =>
        entry is null ? null : await _controller.DeleteHistoryAsync(entry.Id), refresh: true);

    private async void Clear_Click(object sender, RoutedEventArgs e) => await RunAsync(async _ =>
    {
        await _controller.ClearHistoryAsync(this);
        return null;
    }, refresh: true);

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void Entries_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.Key == Key.Enter) { e.Handled = true; Open_Click(sender, e); }
        else if (e.Key == Key.Delete) { e.Handled = true; Delete_Click(sender, e); }
        else if (e.Key == Key.Escape) { e.Handled = true; Close(); }
    }
}
