using System.Windows;
using System.Windows.Threading;
using FileVault.UI.Ipc;
using FileVault.UI.Services;
using FileVault.UI.ViewModels;

namespace FileVault.UI;

public partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }
    private readonly ServiceClient _client;
    private readonly Dispatcher _dispatcher;

    public MainWindow()
    {
        InitializeComponent();
        Icon = AppIcon.Get();

        _dispatcher = Dispatcher;
        _client = new ServiceClient();
        ViewModel = new MainWindowViewModel(_client);

        IconRail.VaultSelected += async vault =>
        {
            try
            {
                Logger.Log($"VaultSelected: {vault.FilePath}");
                ViewModel.ActiveVault = vault;
                var folderVm = new FolderPanelViewModel(_client, vault.FilePath);
                var contentVm = new ContentAreaViewModel(_client, vault.FilePath);

                FolderPanelView.SetViewModel(folderVm);
                ContentAreaView.SetViewModel(contentVm);

                ContentAreaView.FileOpened -= OnFileOpened;
                ContentAreaView.FileOpened += OnFileOpened;

                folderVm.FolderSelected += async path =>
                {
                    try { await contentVm.LoadFolderAsync(path); }
                    catch (Exception ex) { Logger.Log("FolderSelected", ex); }
                };

                FolderPanelView.TreeChanged -= OnFolderTreeChanged;
                FolderPanelView.TreeChanged += OnFolderTreeChanged;

                await folderVm.LoadAsync("/");
                await contentVm.LoadFolderAsync("/");
            }
            catch (Exception ex) { Logger.Log("VaultSelected", ex); }
        };

        // CollectionChanged may fire from background thread — dispatch to UI thread
        ViewModel.Vaults.CollectionChanged += (_, _) =>
        {
            _dispatcher.BeginInvoke(() =>
            {
                IconRail.Vaults.Clear();
                foreach (var v in ViewModel.Vaults)
                    IconRail.Vaults.Add(v);
            });
        };

        IconRail.CreateVaultRequested += async () =>
        {
            try
            {
                Logger.Log("CreateVaultRequested: opening dialog");
                var dialog = new Dialogs.CreateVaultDialog(
                    new PasswordDialogViewModel(_client, ""))
                {
                    Owner = this
                };
                var result = dialog.ShowDialog();
                if (result == true
                    && dialog.VaultPath is { Length: > 0 }
                    && dialog.Password is { Length: > 0 })
                {
                    await ViewModel.CreateVaultAsync(
                        dialog.VaultPath,
                        dialog.DisplayName ?? dialog.VaultPath,
                        dialog.Password,
                        dialog.CoverImageBytes);
                }
            }
            catch (Exception ex) { Logger.Log("CreateVaultRequested", ex); }
        };

        IconRail.OpenVaultRequested += async () =>
        {
            try
            {
                Logger.Log("OpenVaultRequested: opening dialog");
                var dialog = new Dialogs.UnlockVaultDialog { Owner = this };
                if (dialog.ShowDialog() == true
                    && dialog.VaultPath is { Length: > 0 }
                    && dialog.Password is { Length: > 0 })
                {
                    await ViewModel.UnlockVaultAsync(dialog.VaultPath, dialog.Password);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("OpenVaultRequested", ex);
                MessageBox.Show(this, $"Failed to unlock vault:\n{ex.Message}",
                    "FileVault", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        IconRail.LockVaultRequested += async vault =>
        {
            try
            {
                Logger.Log($"LockVaultRequested: {vault.FilePath}");
                var wasActive = ViewModel.ActiveVault?.FilePath == vault.FilePath;
                await ViewModel.LockVaultAsync(vault.FilePath);
                ViewModel.Vaults.Remove(vault);
                if (wasActive)
                {
                    ViewModel.ActiveVault = null;
                    FolderPanelView.Clear();
                    ContentAreaView.Clear();
                    if (ViewerOverlay.Visibility == Visibility.Visible)
                        ViewerOverlay.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex) { Logger.Log("LockVaultRequested", ex); }
        };

        IconRail.EditSettingsRequested += async vault =>
        {
            try
            {
                Logger.Log($"EditSettingsRequested: {vault.FilePath}");
                var dlg = new Dialogs.EditVaultSettingsDialog(vault) { Owner = this };
                if (dlg.ShowDialog() == true && dlg.Changed)
                {
                    var newPath = await _client.UpdateVaultSettingsAsync(
                        vault.FilePath, dlg.CoverImageBytes);
                    vault.FilePath = newPath;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("EditSettingsRequested", ex);
                MessageBox.Show(this, $"Failed to update vault settings:\n{ex.Message}",
                    "FileVault", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        IconRail.ChangePasswordRequested += vault =>
        {
            try
            {
                Logger.Log($"ChangePasswordRequested: {vault.FilePath}");
                var dialog = new Dialogs.ChangePasswordDialog(
                    new PasswordDialogViewModel(_client, vault.FilePath))
                {
                    Owner = this
                };
                dialog.ShowDialog();
            }
            catch (Exception ex) { Logger.Log("ChangePasswordRequested", ex); }
        };

        ViewerOverlay.CloseRequested += () =>
        {
            ViewerOverlay.Visibility = Visibility.Collapsed;
        };
    }

    private async void OnFolderTreeChanged()
    {
        try
        {
            var contentVm = ContentAreaView.ViewModel;
            if (contentVm is not null) await contentVm.LoadFolderAsync(contentVm.CurrentFolder);
        }
        catch (Exception ex) { Logger.Log("OnFolderTreeChanged", ex); }
    }

    private async void OnFileOpened(Models.FileItemModel file)
    {
        try
        {
            if (ViewModel.ActiveVault is null) return;
            var contentVm = ContentAreaView.ViewModel;
            if (contentVm is null) return;

            var playable = contentVm.Items
                .Where(i => !i.IsDirectory && (i.IsImage || i.IsVideo))
                .ToList();
            var index = playable.IndexOf(file);
            if (index < 0) return;

            ViewerOverlay.Visibility = Visibility.Visible;
            await ViewerOverlay.OpenAsync(_client, ViewModel.ActiveVault.FilePath, playable, index);
        }
        catch (Exception ex) { Logger.Log("OnFileOpened", ex); }
    }
}
