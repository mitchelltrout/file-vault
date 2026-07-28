using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileVault.UI.ViewModels;

namespace FileVault.UI.Views;

public partial class LockScreen : UserControl
{
    public LockScreenViewModel? ViewModel { get; private set; }
    public event Func<string, string, Task>? UnlockRequested;

    public LockScreen() => InitializeComponent();

    public void Show(LockScreenViewModel vm)
    {
        ViewModel = vm;
        VaultPathText.Text = vm.VaultPath;
        ErrorText.Visibility = Visibility.Collapsed;
        PasswordInput.Password = "";
        Visibility = Visibility.Visible;
        PasswordInput.Focus();
    }

    public void Hide()
    {
        ViewModel?.Clear();
        Visibility = Visibility.Collapsed;
    }

    private async void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && UnlockRequested is not null && ViewModel is not null)
        {
            try
            {
                await UnlockRequested(ViewModel.VaultPath, PasswordInput.Password);
                Hide();
            }
            catch (Exception ex)
            {
                ErrorText.Text = ex.Message;
                ErrorText.Visibility = Visibility.Visible;
                PasswordInput.Password = "";
            }
        }
    }
}
