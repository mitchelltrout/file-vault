using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FileVault.UI.Ipc;
using FileVault.UI.Models;
using FileVault.UI.Services;
using FileVault.UI.ViewModels;

namespace FileVault.UI.Views;

public partial class FullscreenViewer : UserControl
{
    public FileViewerViewModel? ViewModel { get; private set; }
    public event Action? CloseRequested;

    private IServiceClient? _client;
    private string? _vaultPath;
    private double _naturalWidth;
    private double _naturalHeight;
    private int _rotationDegrees;

    public FullscreenViewer() => InitializeComponent();

    /// <summary>
    /// When hosted inside MediaViewerOverlay, hide the inner chrome (header bar, prev/next, close)
    /// so the overlay's own chrome is used instead. Zoom controls stay visible.
    /// </summary>
    public void HideChrome()
    {
        ChromeBar.Visibility = Visibility.Collapsed;
        PrevButton.Visibility = Visibility.Collapsed;
        NextButton.Visibility = Visibility.Collapsed;
        // Position zoom bar below the overlay's 44px header bar
        ZoomBar.Margin = new Thickness(0, 48, 4, 0);
    }

    public async Task OpenAsync(IServiceClient client, string vaultPath,
        IReadOnlyList<FileItemModel> files, int startIndex)
    {
        _client = client;
        _vaultPath = vaultPath;
        ViewModel = new FileViewerViewModel();
        ViewModel.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(FileViewerViewModel.CurrentFile))
                await LoadCurrentAsync();
        };
        ViewModel.Open(files, startIndex);
        await LoadCurrentAsync();
        Focus();
    }

    private async Task LoadCurrentAsync()
    {
        try
        {
            if (_client is null || _vaultPath is null || ViewModel?.CurrentFile is null)
            {
                MainImage.Source = null;
                return;
            }
            var file = ViewModel.CurrentFile;
            FileNameText.Text = file.Name;
            if (!file.IsImage)
            {
                MainImage.Source = null;
                return;
            }
            var bytes = await _client.ReadFileAsync(_vaultPath, file.VaultPath, 104_857_600);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            MainImage.Source = bmp;
            _naturalWidth = bmp.PixelWidth;
            _naturalHeight = bmp.PixelHeight;
            MainImage.Width = _naturalWidth;
            MainImage.Height = _naturalHeight;
            // Apply saved rotation
            _rotationDegrees = file.RotationDegrees;
            ImageRotation.Angle = _rotationDegrees;
            // Default: fit to screen if image is larger than the viewport, otherwise 1:1
            ApplyInitialScale();
        }
        catch (Exception ex) { Logger.Log("FullscreenViewer.LoadCurrentAsync", ex); }
    }

    private void ApplyInitialScale()
    {
        if (_naturalWidth <= 0 || _naturalHeight <= 0) return;
        var availW = ImageScroller.ViewportWidth > 0 ? ImageScroller.ViewportWidth : ImageScroller.ActualWidth;
        var availH = ImageScroller.ViewportHeight > 0 ? ImageScroller.ViewportHeight : ImageScroller.ActualHeight;
        if (availW <= 0 || availH <= 0) { SetScale(1); return; }

        // When rotated 90° or 270°, effective dimensions are swapped
        var isRotated90 = _rotationDegrees % 180 != 0;
        var effectiveW = isRotated90 ? _naturalHeight : _naturalWidth;
        var effectiveH = isRotated90 ? _naturalWidth : _naturalHeight;

        if (effectiveW <= availW && effectiveH <= availH)
            SetScale(1);
        else
            SetScale(Math.Min(availW / effectiveW, availH / effectiveH));
    }

    private void SetScale(double scale)
    {
        scale = Math.Clamp(scale, 0.05, 16);
        ImageScale.ScaleX = scale;
        ImageScale.ScaleY = scale;
        ZoomText.Text = $"{scale * 100:F0}%";
    }

    private double CurrentScale => ImageScale.ScaleX;

    private void ImageScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Only auto-fit when the image is larger than the viewport at 100%.
        // Leave user zoom alone otherwise.
        if (_naturalWidth > 0 && _naturalHeight > 0
            && (_naturalWidth > e.NewSize.Width || _naturalHeight > e.NewSize.Height))
        {
            // Recompute only if the current scale still corresponds to "fit"
            var fit = Math.Min(e.NewSize.Width / _naturalWidth, e.NewSize.Height / _naturalHeight);
            if (CurrentScale < 1.0 && Math.Abs(CurrentScale - fit) > 0.25)
                SetScale(fit);
        }
    }

    private void RotateLeft_Click(object sender, RoutedEventArgs e) => ApplyRotation(-90);
    private void RotateRight_Click(object sender, RoutedEventArgs e) => ApplyRotation(90);

    private void ApplyRotation(int delta)
    {
        _rotationDegrees = ((_rotationDegrees + delta) % 360 + 360) % 360;
        ImageRotation.Angle = _rotationDegrees;
        ApplyInitialScale();
        PersistRotationAsync();
    }

    private async void PersistRotationAsync()
    {
        try
        {
            if (_client is null || _vaultPath is null || ViewModel?.CurrentFile is null) return;
            var file = ViewModel.CurrentFile;
            file.RotationDegrees = _rotationDegrees;
            await _client.SetRotationAsync(_vaultPath, file.VaultPath, _rotationDegrees);
        }
        catch (Exception ex) { Logger.Log("FullscreenViewer.PersistRotation", ex); }
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetScale(CurrentScale * 1.25);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetScale(CurrentScale / 1.25);
    private void ActualSize_Click(object sender, RoutedEventArgs e) => SetScale(1);
    private void FitScreen_Click(object sender, RoutedEventArgs e) => ApplyInitialScale();

    private void Viewer_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control || _naturalWidth > 0)
        {
            SetScale(CurrentScale * (e.Delta > 0 ? 1.15 : 1 / 1.15));
            e.Handled = true;
        }
    }

    private void Prev_Click(object sender, RoutedEventArgs e) =>
        ViewModel?.PreviousCommand.Execute(null);

    private void Next_Click(object sender, RoutedEventArgs e) =>
        ViewModel?.NextCommand.Execute(null);

    private void Close_Click(object sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke();

    private void Viewer_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                ViewModel?.PreviousCommand.Execute(null);
                break;
            case Key.Right:
                ViewModel?.NextCommand.Execute(null);
                break;
            case Key.Escape:
                CloseRequested?.Invoke();
                break;
            case Key.OemPlus:
            case Key.Add:
                SetScale(CurrentScale * 1.25);
                break;
            case Key.OemMinus:
            case Key.Subtract:
                SetScale(CurrentScale / 1.25);
                break;
            case Key.D0:
            case Key.NumPad0:
                SetScale(1);
                break;
            case Key.OemOpenBrackets when Keyboard.Modifiers == ModifierKeys.Control:
                ApplyRotation(-90);
                e.Handled = true;
                break;
            case Key.OemCloseBrackets when Keyboard.Modifiers == ModifierKeys.Control:
                ApplyRotation(90);
                e.Handled = true;
                break;
        }
    }
}
