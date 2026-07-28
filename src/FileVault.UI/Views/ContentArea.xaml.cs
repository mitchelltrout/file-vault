using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FileVault.UI.Models;
using FileVault.UI.Services;
using FileVault.UI.ViewModels;
using Microsoft.Win32;

namespace FileVault.UI.Views;

public partial class ContentArea : UserControl
{
    public ContentAreaViewModel? ViewModel { get; private set; }
    public event Action<FileItemModel>? FileOpened;

    public ContentArea()
    {
        InitializeComponent();
        GridListToggle.IsChecked = true;
    }

    public void SetViewModel(ContentAreaViewModel vm)
    {
        ViewModel = vm;
        FileGrid.ItemsSource = vm.Items;
        FileList.ItemsSource = vm.Items;
    }

    public void Clear()
    {
        ViewModel = null;
        FileGrid.ItemsSource = null;
        FileList.ItemsSource = null;
    }

    private void Toggle_Checked(object sender, RoutedEventArgs e)
    {
        FileGrid.Visibility = Visibility.Visible;
        FileList.Visibility = Visibility.Collapsed;
        if (ToggleGlyph is not null) ToggleGlyph.Text = "\uE8A9"; // grid view glyph
    }

    private void Toggle_Unchecked(object sender, RoutedEventArgs e)
    {
        FileGrid.Visibility = Visibility.Collapsed;
        FileList.Visibility = Visibility.Visible;
        if (ToggleGlyph is not null) ToggleGlyph.Text = "\uEA37"; // list view glyph
    }

    private void Item_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        var item = (sender is ListBox lb ? lb.SelectedItem : null) as FileItemModel;
        if (item is not null) FileOpened?.Invoke(item);
    }

    private IEnumerable<FileItemModel> CurrentSelection()
    {
        var lb = FileGrid.Visibility == Visibility.Visible ? FileGrid : FileList;
        return lb.SelectedItems.OfType<FileItemModel>().ToList();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Log("Delete_Click");
            if (ViewModel is null) { Logger.Log("Delete_Click: ViewModel is null"); return; }
            var sel = CurrentSelection();
            if (sel.Count() == 0) return;
            ViewModel.SelectedItems.Clear();
            foreach (var s in sel) ViewModel.SelectedItems.Add(s);
            await ViewModel.DeleteSelectedAsync();
        }
        catch (Exception ex) { Logger.Log("Delete_Click", ex); }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Log("Export_Click");
            if (ViewModel is null) { Logger.Log("Export_Click: ViewModel is null"); return; }
            var sel = CurrentSelection();
            if (sel.Count() == 0) return;
            var picker = new OpenFolderDialog();
            if (picker.ShowDialog(Window.GetWindow(this)) == true)
            {
                ViewModel.SelectedItems.Clear();
                foreach (var s in sel) ViewModel.SelectedItems.Add(s);
                await ViewModel.ExportSelectedAsync(picker.FolderName);
            }
        }
        catch (Exception ex) { Logger.Log("Export_Click", ex); }
    }

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Log("AddFiles_Click");
            if (ViewModel is null) { Logger.Log("AddFiles_Click: ViewModel is null"); return; }
            var picker = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "All files (*.*)|*.*",
            };
            if (picker.ShowDialog(Window.GetWindow(this)) == true)
                await ViewModel.ImportFilesAsync(picker.FileNames, "KeepBoth");
        }
        catch (Exception ex) { Logger.Log("AddFiles_Click", ex); }
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (ViewModel is null) return;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                await ViewModel.ImportFilesAsync(paths, "KeepBoth");
            }
        }
        catch (Exception ex) { Logger.Log("Grid_Drop", ex); }
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private System.Windows.Point _dragStart;
    private bool _dragReady;

    private void Item_PreviewLeftDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragReady = true;
    }

    private void Item_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragReady || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var sel = CurrentSelection().ToList();
        if (sel.Count == 0) return;

        _dragReady = false;
        var data = new DataObject(FolderPanel.VaultItemDragFormat,
            sel.Select(s => s.VaultPath).ToArray());
        try { DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move); }
        catch (Exception ex) { Logger.Log("Item_DragStart", ex); }
    }

    // Right-clicking an unselected item should select it; if it's already part of
    // a multi-selection, leave the selection alone so the menu acts on the whole set.
    private void Item_PreviewRightDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem lbi) return;
        if (!lbi.IsSelected)
        {
            var lb = FileGrid.Visibility == Visibility.Visible ? FileGrid : FileList;
            lb.SelectedItems.Clear();
            lbi.IsSelected = true;
        }
    }

    private void OpenMenu_Click(object sender, RoutedEventArgs e)
    {
        var item = CurrentSelection().FirstOrDefault();
        if (item is not null) FileOpened?.Invoke(item);
    }

    private async void RenameMenu_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel is null) return;
            var item = CurrentSelection().FirstOrDefault();
            if (item is null) return;
            var dlg = new Dialogs.RenameDialog(item.Name) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.NewName)
                && dlg.NewName != item.Name)
            {
                await ViewModel.RenameAsync(item, dlg.NewName);
            }
        }
        catch (Exception ex) { Logger.Log("RenameMenu_Click", ex); }
    }
}
