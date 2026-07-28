using CommunityToolkit.Mvvm.ComponentModel;
using FileVault.UI.Ipc;
using System.Collections.ObjectModel;

namespace FileVault.UI.ViewModels;

public partial class FolderItemViewModel : ObservableObject
{
    public string FullPath { get; set; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;

    public ObservableCollection<FolderItemViewModel> Children { get; } = [];

    public FolderItemViewModel(string name, string fullPath)
    {
        _name = name;
        FullPath = fullPath;
    }
}

public partial class FolderPanelViewModel(IServiceClient client, string vaultPath) : ObservableObject
{
    public ObservableCollection<FolderItemViewModel> RootFolders { get; } = [];
    public event Action<string>? FolderSelected;

    [ObservableProperty]
    private string _currentPath = "/";

    /// <summary>Load the full folder tree under <paramref name="path"/> recursively.</summary>
    public async Task LoadAsync(string path = "/")
    {
        RootFolders.Clear();
        var root = new FolderItemViewModel("Root", "/") { IsExpanded = true };
        await PopulateChildrenAsync(root);
        RootFolders.Add(root);
    }

    public async Task RefreshAsync()
    {
        var expanded = new HashSet<string>();
        var selected = CurrentPath;
        if (RootFolders.Count > 0)
            CollectExpanded(RootFolders[0], expanded);
        await LoadAsync();
        if (RootFolders.Count > 0)
            RestoreState(RootFolders[0], expanded, selected);
    }

    private static void CollectExpanded(FolderItemViewModel node, HashSet<string> acc)
    {
        if (node.IsExpanded) acc.Add(node.FullPath);
        foreach (var c in node.Children) CollectExpanded(c, acc);
    }

    private static void RestoreState(FolderItemViewModel node, HashSet<string> expanded, string selected)
    {
        if (expanded.Contains(node.FullPath)) node.IsExpanded = true;
        if (node.FullPath == selected) node.IsSelected = true;
        foreach (var c in node.Children) RestoreState(c, expanded, selected);
    }

    private async Task PopulateChildrenAsync(FolderItemViewModel parent)
    {
        var response = await client.ListFolderAsync(vaultPath, parent.FullPath);
        parent.Children.Clear();
        foreach (var node in response.Nodes.Where(n => n.IsDirectory))
        {
            var fullPath = parent.FullPath.TrimEnd('/') + "/" + node.Name;
            var child = new FolderItemViewModel(node.Name, fullPath);
            parent.Children.Add(child);
            await PopulateChildrenAsync(child);
        }
    }

    public async Task SelectFolderAsync(string path)
    {
        CurrentPath = path;
        FolderSelected?.Invoke(path);
        await Task.CompletedTask;
    }

    public async Task CreateFolderAsync(string parentPath, string name)
    {
        var full = parentPath.TrimEnd('/') + "/" + name;
        await client.CreateFolderAsync(vaultPath, full);
        await RefreshAsync();
    }

    public async Task RenameFolderAsync(string path, string newName)
    {
        await client.RenameAsync(vaultPath, path, newName);
        await RefreshAsync();
    }

    public async Task DeleteFolderAsync(string path)
    {
        await client.DeleteAsync(vaultPath, path);
        await RefreshAsync();
    }

    public async Task MoveItemIntoAsync(string sourcePath, string destFolder)
    {
        await client.MoveAsync(vaultPath, sourcePath, destFolder);
        await RefreshAsync();
    }
}
