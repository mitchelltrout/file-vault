using CommunityToolkit.Mvvm.ComponentModel;
using FileVault.UI.Ipc;
using FileVault.UI.Models;
using System.Collections.ObjectModel;

namespace FileVault.UI.ViewModels;

public partial class MainWindowViewModel(IServiceClient client) : ObservableObject
{
    [ObservableProperty]
    private VaultInfo? _activeVault;

    public ObservableCollection<VaultInfo> Vaults { get; } = [];

    public async Task UnlockVaultAsync(string filePath, string password)
    {
        var response = await client.UnlockVaultAsync(filePath, password);
        var existing = Vaults.FirstOrDefault(v => v.FilePath == filePath);
        if (existing is not null)
        {
            existing.IsUnlocked = true;
            existing.DisplayName = response.DisplayName;
        }
        else
        {
            existing = new VaultInfo
            {
                FilePath = filePath,
                DisplayName = response.DisplayName,
                IsUnlocked = true
            };
            Vaults.Add(existing);
        }
        ActiveVault = existing;
    }

    public async Task LockVaultAsync(string filePath)
    {
        await client.LockVaultAsync(filePath);
        var vault = Vaults.FirstOrDefault(v => v.FilePath == filePath);
        if (vault is not null) vault.IsUnlocked = false;
        if (ActiveVault?.FilePath == filePath)
            ActiveVault = Vaults.FirstOrDefault(v => v.IsUnlocked);
    }

    public async Task CreateVaultAsync(string filePath, string displayName, string password,
        byte[]? coverImageBytes = null)
    {
        await client.CreateVaultAsync(filePath, displayName, password, coverImageBytes);
        await UnlockVaultAsync(filePath, password);
    }
}
