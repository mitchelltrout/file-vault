using System.Windows;
using FileVault.UI.Services;
using Microsoft.Win32;

namespace FileVault.UI.Dialogs;

public partial class UnlockVaultDialog : Window
{
    public string? VaultPath { get; private set; }
    public string? Password { get; private set; }

    public UnlockVaultDialog(string? initialPath = null)
    {
        InitializeComponent();
        if (!string.IsNullOrEmpty(initialPath))
            FilePathBox.Text = initialPath;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new OpenFileDialog
            {
                Filter = "Vault File (*.vault)|*.vault|All files (*.*)|*.*",
                CheckFileExists = true,
            };
            if (picker.ShowDialog(this) == true)
                FilePathBox.Text = picker.FileName;
        }
        catch (Exception ex) { Logger.Log("UnlockVaultDialog.Browse_Click", ex); }
    }

    private void OnUnlock(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FilePathBox.Text))
        {
            ErrorText.Text = "Please choose a vault file.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        if (PasswordBox.Password.Length == 0)
        {
            ErrorText.Text = "Please enter the vault password.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        VaultPath = FilePathBox.Text;
        Password = PasswordBox.Password;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
