using System.Windows;

namespace FileVault.UI.Dialogs;

public partial class DeleteConfirmDialog : Window
{
    public DeleteConfirmDialog(string description)
    {
        InitializeComponent();
        DescriptionText.Text = description;
    }

    private void Delete_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
