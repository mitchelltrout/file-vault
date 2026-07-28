using CommunityToolkit.Mvvm.ComponentModel;

namespace FileVault.UI.ViewModels;

public partial class LockScreenViewModel(string vaultPath) : ObservableObject
{
    public string VaultPath { get; } = vaultPath;

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string? _errorMessage;

    public void Clear()
    {
        Password = "";
        ErrorMessage = null;
    }
}
