using System.Windows;
using System.Windows.Controls;
using FileVault.UI.Services;
using FileVault.UI.ViewModels;

namespace FileVault.UI.Views;

public partial class FolderPanel : UserControl
{
    public FolderPanelViewModel? ViewModel { get; private set; }
    public event Action<string>? FolderSelected;
    public event Action? TreeChanged;

    public const string VaultItemDragFormat = "FileVault.VaultItemPath";

    public FolderPanel() => InitializeComponent();

    public void SetViewModel(FolderPanelViewModel vm)
    {
        ViewModel = vm;
        FolderTree.ItemsSource = vm.RootFolders;
    }

    public void Clear()
    {
        ViewModel = null;
        FolderTree.ItemsSource = null;
    }

    private async void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderItemViewModel folder && ViewModel is not null)
        {
            await ViewModel.SelectFolderAsync(folder.FullPath);
            FolderSelected?.Invoke(folder.FullPath);
        }
    }

    private FolderItemViewModel? GetContextFolder(object sender)
    {
        if (sender is MenuItem mi && mi.DataContext is FolderItemViewModel f) return f;
        if (sender is TreeViewItem tvi && tvi.DataContext is FolderItemViewModel f2) return f2;
        // Fall back to current selection
        return FolderTree.SelectedItem as FolderItemViewModel;
    }

    private async void NewRootFolder_Click(object sender, RoutedEventArgs e)
    {
        await CreateFolderUnderAsync("/");
    }

    private async void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        var target = GetContextFolder(sender);
        if (target is null) return;
        await CreateFolderUnderAsync(target.FullPath);
    }

    private async Task CreateFolderUnderAsync(string parentPath)
    {
        try
        {
            if (ViewModel is null) return;
            var dlg = new Dialogs.RenameDialog("New Folder") { Owner = Window.GetWindow(this), Title = "New Folder" };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.NewName))
            {
                await ViewModel.CreateFolderAsync(parentPath, dlg.NewName);
                TreeChanged?.Invoke();
            }
        }
        catch (Exception ex) { Logger.Log("CreateFolder", ex); }
    }

    private async void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var target = GetContextFolder(sender);
            if (target is null || ViewModel is null || target.FullPath == "/") return;
            var dlg = new Dialogs.RenameDialog(target.Name) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.NewName) && dlg.NewName != target.Name)
            {
                await ViewModel.RenameFolderAsync(target.FullPath, dlg.NewName);
                TreeChanged?.Invoke();
            }
        }
        catch (Exception ex) { Logger.Log("RenameFolder", ex); }
    }

    private async void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var target = GetContextFolder(sender);
            if (target is null || ViewModel is null || target.FullPath == "/") return;
            var result = MessageBox.Show(Window.GetWindow(this),
                $"Delete folder '{target.Name}' and everything inside it?",
                "FileVault", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                await ViewModel.DeleteFolderAsync(target.FullPath);
                TreeChanged?.Invoke();
            }
        }
        catch (Exception ex) { Logger.Log("DeleteFolder", ex); }
    }

    private void FolderItem_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(VaultItemDragFormat)
            ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void FolderItem_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (sender is not TreeViewItem tvi || tvi.DataContext is not FolderItemViewModel target) return;
            if (ViewModel is null) return;
            if (e.Data.GetData(VaultItemDragFormat) is not string[] paths) return;
            foreach (var p in paths)
                await ViewModel.MoveItemIntoAsync(p, target.FullPath);
            TreeChanged?.Invoke();
            e.Handled = true;
        }
        catch (Exception ex) { Logger.Log("FolderItem_Drop", ex); }
    }
}
