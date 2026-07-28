using System.Windows;
using FileVault.UI.ViewModels;

namespace FileVault.UI.Dialogs;

public partial class ChangePasswordDialog : Window
{
    private readonly PasswordDialogViewModel _vm;

    public ChangePasswordDialog(PasswordDialogViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        Generator.SetViewModel(vm);
        Generator.PasswordSelected += p =>
        {
            NewPasswordBox.Password = p;
            ConfirmPasswordBox.Password = p;
        };
        AdvancedToggle.Checked += (_, _) => AdvancedChevron.Text = "\u25BE";
        AdvancedToggle.Unchecked += (_, _) => AdvancedChevron.Text = "\u25B8";
    }

    private async void OnSubmit(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.CurrentPassword = CurrentPasswordBox.Password;
            _vm.NewPassword = NewPasswordBox.Password;
            _vm.ConfirmPassword = ConfirmPasswordBox.Password;
            await _vm.SubmitAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
