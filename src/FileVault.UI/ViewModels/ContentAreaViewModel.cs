using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileVault.UI.Ipc;
using FileVault.UI.Models;
using FileVault.UI.Services;

namespace FileVault.UI.ViewModels;

public partial class ContentAreaViewModel : ObservableObject
{
    private readonly IServiceClient _client;
    private readonly string _vaultPath;
    private readonly ThumbnailService _thumbnails = new();

    [ObservableProperty]
    private bool _isGridView = true;

    [ObservableProperty]
    private string _currentFolder = "/";

    public ObservableCollection<FileItemModel> Items { get; } = [];
    public ObservableCollection<FileItemModel> SelectedItems { get; } = [];

    public ContentAreaViewModel(IServiceClient client, string vaultPath)
    {
        _client = client;
        _vaultPath = vaultPath;
    }

    public async Task LoadFolderAsync(string folderPath)
    {
        CurrentFolder = folderPath;
        var response = await _client.ListFolderAsync(_vaultPath, folderPath);
        Items.Clear();
        SelectedItems.Clear();
        var folderBase = folderPath.TrimEnd('/');
        foreach (var node in response.Nodes)
        {
            var model = new FileItemModel
            {
                Name = node.Name,
                IsDirectory = node.IsDirectory,
                PlaintextLength = node.PlaintextLength,
                ModifiedAt = DateTimeOffset.FromUnixTimeSeconds(node.ModifiedAtUtc),
                VaultPath = folderBase + "/" + node.Name
            };
            if (!model.IsDirectory)
            {
                model.IsImage = FileItemModel.ImageExtensions.Contains(model.Extension);
                model.IsVideo = FileItemModel.VideoExtensions.Contains(model.Extension);
                model.RotationDegrees = node.RotationDegrees;
            }
            Items.Add(model);
        }

        // Kick off thumbnail loads on background tasks — they marshal back to UI thread via observable props
        _ = Task.Run(() => LoadThumbnailsAsync(Items.ToList()));
    }

    private async Task LoadThumbnailsAsync(List<FileItemModel> items)
    {
        foreach (var item in items.Where(i => i.IsImage))
        {
            try
            {
                var bytes = await _client.ReadFileAsync(_vaultPath, item.VaultPath, 10_485_760 /* 10 MB for thumbs */);
                var bmp = await _thumbnails.GetThumbnailAsync(bytes, item.VaultPath,
                    rotationDegrees: item.RotationDegrees);
                if (bmp is not null)
                {
                    // SynchronizationContext on WPF auto-marshals; assigning observable property triggers UI update
                    _ = Application.Current.Dispatcher.BeginInvoke(() => item.Thumbnail = bmp);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Thumbnail load failed for {item.VaultPath}", ex);
            }
        }
    }

    [RelayCommand]
    private void ToggleView() => IsGridView = !IsGridView;

    public async Task DeleteSelectedAsync()
    {
        foreach (var item in SelectedItems.ToList())
            await _client.DeleteAsync(_vaultPath, item.VaultPath);
        await LoadFolderAsync(CurrentFolder);
    }

    public async Task ImportFilesAsync(IEnumerable<string> sourcePaths, string collisionBehavior)
    {
        await _client.ImportFilesAsync(_vaultPath, CurrentFolder, sourcePaths, collisionBehavior);
        await LoadFolderAsync(CurrentFolder);
    }

    public async Task RenameAsync(FileItemModel item, string newName)
    {
        await _client.RenameAsync(_vaultPath, item.VaultPath, newName);
        await LoadFolderAsync(CurrentFolder);
    }

    public async Task MoveAsync(FileItemModel item, string destFolder)
    {
        await _client.MoveAsync(_vaultPath, item.VaultPath, destFolder);
        await LoadFolderAsync(CurrentFolder);
    }

    public async Task ExportSelectedAsync(string destDir)
    {
        foreach (var item in SelectedItems)
            await _client.ExportAsync(_vaultPath, item.VaultPath, destDir);
    }
}
