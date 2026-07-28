using System.IO;
using System.Windows;
using FileVault.UI.Services;
using FileVault.UI.ViewModels;
using Microsoft.Win32;

namespace FileVault.UI.Dialogs;

public partial class CreateVaultDialog : Window
{
    public string? VaultPath { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Password { get; private set; }
    public byte[]? CoverImageBytes { get; private set; }

    private readonly PasswordDialogViewModel _vm;

    public CreateVaultDialog(PasswordDialogViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        Generator.SetViewModel(vm);
        Generator.PasswordSelected += p => PasswordBox.Password = p;
        AdvancedToggle.Checked += (_, _) => AdvancedChevron.Text = "\u25BE";
        AdvancedToggle.Unchecked += (_, _) => AdvancedChevron.Text = "\u25B8";
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new SaveFileDialog
            {
                FileName = "MyVault",
                DefaultExt = ".vault",
                Filter = "Vault File (*.vault)|*.vault",
            };
            if (picker.ShowDialog(this) == true)
            {
                FilePathBox.Text = picker.FileName;
            }
        }
        catch (Exception ex)
        {
            Logger.Log("Browse_Click", ex);
        }
    }

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        VaultPath = FilePathBox.Text;
        DisplayName = DisplayNameBox.Text;
        Password = PasswordBox.Password;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
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
            CoverImagePathText.Text = Path.GetFileName(dlg.FileName);
        }
        catch (Exception ex)
        {
            Logger.Log("BrowseCover_Click", ex);
            MessageBox.Show(this, ex.Message, "Cover image",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ClearCover_Click(object sender, RoutedEventArgs e)
    {
        CoverImageBytes = null;
        CoverImagePathText.Text = "(none)";
    }
}
