using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FileVault.UI.Models;

namespace FileVault.UI.Views;

public partial class VaultIconRail : UserControl
{
    public ObservableCollection<VaultInfo> Vaults { get; } = [];
    public event Action<VaultInfo>? VaultSelected;
    public event Action? CreateVaultRequested;
    public event Action? OpenVaultRequested;
    public event Action<VaultInfo>? LockVaultRequested;
    public event Action<VaultInfo>? ChangePasswordRequested;
    public event Action<VaultInfo>? EditSettingsRequested;

    public VaultIconRail()
    {
        InitializeComponent();
        VaultList.ItemsSource = Vaults;
    }

    private void VaultButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VaultInfo vault)
            VaultSelected?.Invoke(vault);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu is not null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = PlacementMode.Top;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void CreateVault_Click(object sender, RoutedEventArgs e) =>
        CreateVaultRequested?.Invoke();

    private void OpenVault_Click(object sender, RoutedEventArgs e) =>
        OpenVaultRequested?.Invoke();

    private void LockVault_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is VaultInfo vault)
            LockVaultRequested?.Invoke(vault);
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is VaultInfo vault)
            ChangePasswordRequested?.Invoke(vault);
    }

    private void EditSettings_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is VaultInfo vault)
            EditSettingsRequested?.Invoke(vault);
    }
}
