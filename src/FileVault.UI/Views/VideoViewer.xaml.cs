using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FileVault.UI.Ipc;
using FileVault.UI.Models;
using FileVault.UI.ViewModels;

namespace FileVault.UI.Views;

public partial class VideoViewer : UserControl, IDisposable
{
    public VideoViewerViewModel? ViewModel { get; private set; }
    private bool _seekDragging;

    public VideoViewer()
    {
        InitializeComponent();
        VideoHost.Loaded += VideoHost_Loaded;
    }

    private void VideoHost_Loaded(object sender, RoutedEventArgs e)
    {
        // LibVLCSharp.WPF's VideoView creates a foreground overlay Window
        // whose default background can flash white. Paint all owned windows black,
        // and also walk the VideoView's visual tree for any panels with
        // white/null backgrounds.
        var hostWindow = Window.GetWindow(this);
        if (hostWindow != null)
        {
            foreach (Window w in hostWindow.OwnedWindows)
                w.Background = System.Windows.Media.Brushes.Black;
        }
        PaintPanelsBlack(VideoHost);
    }

    private static void PaintPanelsBlack(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Panel panel && panel.Name != "OverlayHost")
            {
                if (panel.Background is null ||
                    (panel.Background is SolidColorBrush scb &&
                     (scb.Color == Colors.White || scb.Color == Colors.Transparent)))
                    panel.Background = System.Windows.Media.Brushes.Black;
            }
            PaintPanelsBlack(child);
        }
    }

    /// <summary>
    /// Adds a UIElement into the VideoView's foreground overlay layer so it renders
    /// above the HWND video surface (solves the WPF/HWND airspace problem).
    /// Used by MediaViewerOverlay to inject its header and nav buttons.
    /// </summary>
    public void AddOverlayElement(UIElement element)
    {
        OverlayHost.Children.Add(element);
    }

    public void Open(IServiceClient client, string vaultPath, FileItemModel file)
    {
        Dispose();
        ViewModel = new VideoViewerViewModel();
        ViewModel.PropertyChanged += (_, e) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (ViewModel == null) return;
                if (e.PropertyName == nameof(VideoViewerViewModel.IsPlaying))
                    PlayPauseGlyph.Text = ViewModel.IsPlaying ? "\uE769" : "\uE768";
                if (e.PropertyName == nameof(VideoViewerViewModel.PositionMs) && !_seekDragging)
                    UpdateSlider();
                if (e.PropertyName == nameof(VideoViewerViewModel.DurationMs))
                    UpdateSlider();
            });
        };
        ViewModel.Open(client, vaultPath, file);
        VideoHost.MediaPlayer = ViewModel.Player;
        Focus();
    }

    private void UpdateSlider()
    {
        if (ViewModel == null) return;
        SeekSlider.Maximum = Math.Max(1, ViewModel.DurationMs);
        SeekSlider.Value = ViewModel.PositionMs;
        TimeText.Text = $"{Format(ViewModel.PositionMs)} / {Format(ViewModel.DurationMs)}";
    }

    private static string Format(long ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalHours >= 1 ? t.ToString(@"hh\:mm\:ss") : t.ToString(@"mm\:ss");
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e) => ViewModel?.TogglePlay();

    private void SeekSlider_DragStarted(object sender, DragStartedEventArgs e) => _seekDragging = true;

    private void SeekSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _seekDragging = false;
        ViewModel?.Seek((long)SeekSlider.Value);
    }

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_seekDragging || ViewModel == null) return;
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ViewModel != null) ViewModel.Volume = (int)e.NewValue;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel == null) return;
        switch (e.Key)
        {
            case Key.Space:
                ViewModel.TogglePlay();
                e.Handled = true;
                break;
            case Key.Left:
                ViewModel.Seek(Math.Max(0, ViewModel.PositionMs - 5000));
                e.Handled = true;
                break;
            case Key.Right:
                ViewModel.Seek(Math.Min(ViewModel.DurationMs, ViewModel.PositionMs + 5000));
                e.Handled = true;
                break;
        }
    }

    public void Dispose()
    {
        if (VideoHost != null) VideoHost.MediaPlayer = null;
        ViewModel?.Dispose();
        ViewModel = null;
    }
}
