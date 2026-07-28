using System.IO;
using System.Windows;
using FileVault.UI.Models;
using FileVault.UI.Services;
using Microsoft.Win32;

namespace FileVault.UI.Dialogs;

public partial class EditVaultSettingsDialog : Window
{
    /// <summary>
    /// The selected cover image bytes, or null if unchanged, or empty array if the user chose "Remove".
    /// Callers check <see cref="Changed"/> to know whether the user made a selection.
    /// </summary>
    public byte[]? CoverImageBytes { get; private set; }

    /// <summary>True when the user explicitly chose a new cover or removed the existing one.</summary>
    public bool Changed { get; private set; }

    public EditVaultSettingsDialog(VaultInfo vault)
    {
        InitializeComponent();
        Title = $"Edit settings — {vault.DisplayName}";
    }

    private void BrowseCover_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JPEG image (*.jpg;*.jpeg)|*.jpg;*.jpeg"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var bytes = File.ReadAllBytes(dlg.FileName);
            CoverImageValidator.Validate(bytes);
            CoverImageBytes = bytes;
            Changed = true;
            CoverImagePathText.Text = Path.GetFileName(dlg.FileName);
        }
        catch (Exception ex)
        {
            Logger.Log("EditVaultSettings.BrowseCover", ex);
            MessageBox.Show(this, ex.Message, "Cover image",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RemoveCover_Click(object sender, RoutedEventArgs e)
    {
        CoverImageBytes = [];
        Changed = true;
        CoverImagePathText.Text = "(removed)";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
