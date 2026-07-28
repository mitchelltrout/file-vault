using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileVault.UI.Ipc;
using FileVault.UI.Models;

namespace FileVault.UI.Views;

public partial class MediaViewerOverlay : UserControl
{
    public event Action? CloseRequested;

    private IServiceClient? _client;
    private string? _vaultPath;
    private List<FileItemModel> _items = [];
    private int _index;

    // Video chrome injected into the VideoView's foreground overlay
    private Grid? _videoChrome;

    public MediaViewerOverlay() => InitializeComponent();

    public Task OpenAsync(IServiceClient client, string vaultPath,
        IReadOnlyList<FileItemModel> playableItems, int startIndex)
    {
        _client = client;
        _vaultPath = vaultPath;
        _items = playableItems.ToList();
        _index = Math.Clamp(startIndex, 0, _items.Count - 1);
        return ShowCurrentAsync();
    }

    private async Task ShowCurrentAsync()
    {
        DisposeInner();
        if (_items.Count == 0 || _client == null || _vaultPath == null) return;

        var item = _items[_index];
        FileNameText.Text = item.Name;

        if (item.IsImage)
        {
            // Show the XAML chrome panel normally (no airspace issue for images)
            ChromePanel.Visibility = Visibility.Visible;

            var img = new FullscreenViewer();
            img.HideChrome();
            InnerHost.Content = img;
            await img.OpenAsync(_client, _vaultPath, _items.Where(i => i.IsImage).ToList(),
                _items.Where(i => i.IsImage).ToList().IndexOf(item));
        }
        else if (item.IsVideo)
        {
            // Hide the XAML chrome panel — it would be covered by the HWND.
            // Instead, inject equivalent chrome into the VideoView's foreground overlay.
            ChromePanel.Visibility = Visibility.Collapsed;

            var vid = new VideoViewer();
            InnerHost.Content = vid;

            _videoChrome = BuildVideoChrome(item.Name);
            vid.AddOverlayElement(_videoChrome);

            vid.Open(_client, _vaultPath, item);
        }

        Focus();
    }

    /// <summary>
    /// Builds a Grid containing header (filename + close) and prev/next nav buttons,
    /// to be injected into the VideoView's foreground overlay layer.
    /// </summary>
    private Grid BuildVideoChrome(string fileName)
    {
        var grid = new Grid();

        // Header bar
        var header = new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0xCC, 0, 0, 0)),
            Padding = new Thickness(8),
            Height = 44
        };
        var headerGrid = new Grid();

        var fileNameText = new TextBlock
        {
            Text = fileName,
            Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(8, 0, 0, 0)
        };
        headerGrid.Children.Add(fileNameText);

        var closeBtn = new Button
        {
            Style = (Style)FindResource("FlatButton"),
            Width = 32, Height = 28,
            HorizontalAlignment = HorizontalAlignment.Right,
            ToolTip = "Close (Esc)"
        };
        closeBtn.Content = new TextBlock
        {
            Text = "\uE711",
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush")
        };
        closeBtn.Click += Close_Click;
        headerGrid.Children.Add(closeBtn);

        header.Child = headerGrid;
        grid.Children.Add(header);

        // Prev button
        var prevBtn = new Button
        {
            Style = (Style)FindResource("FlatButton"),
            Width = 44, Height = 80,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x66, 0, 0, 0))
        };
        prevBtn.Content = new TextBlock
        {
            Text = "\uE76B",
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush")
        };
        prevBtn.Click += Prev_Click;
        grid.Children.Add(prevBtn);

        // Next button
        var nextBtn = new Button
        {
            Style = (Style)FindResource("FlatButton"),
            Width = 44, Height = 80,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x66, 0, 0, 0))
        };
        nextBtn.Content = new TextBlock
        {
            Text = "\uE76C",
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush")
        };
        nextBtn.Click += Next_Click;
        grid.Children.Add(nextBtn);

        return grid;
    }

    private void DisposeInner()
    {
        _videoChrome = null;
        if (InnerHost.Content is IDisposable d) d.Dispose();
        InnerHost.Content = null;
    }

    private async void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0) return;
        _index = (_index - 1 + _items.Count) % _items.Count;
        await ShowCurrentAsync();
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0) return;
        _index = (_index + 1) % _items.Count;
        await ShowCurrentAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DisposeInner();
        CloseRequested?.Invoke();
    }

    private async void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close_Click(sender, e);
                e.Handled = true;
                break;
            case Key.Left:
                Prev_Click(sender, e);
                e.Handled = true;
                break;
            case Key.Right:
                Next_Click(sender, e);
                e.Handled = true;
                break;
        }
    }
}
