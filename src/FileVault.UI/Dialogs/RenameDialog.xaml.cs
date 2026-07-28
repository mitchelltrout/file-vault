using System.Windows;

namespace FileVault.UI.Dialogs;

public partial class RenameDialog : Window
{
    public string NewName { get; private set; } = "";

    public RenameDialog(string initialName)
    {
        InitializeComponent();
        NameBox.Text = initialName;
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        NewName = NameBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
