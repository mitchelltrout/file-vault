using System.Windows;

namespace FileVault.UI.Dialogs;

public enum CollisionChoice { Replace, KeepBoth, Skip, Cancel }

public partial class CollisionResolutionDialog : Window
{
    public CollisionChoice Choice { get; private set; } = CollisionChoice.Cancel;
    public bool ApplyToAll => ApplyToAllCheck.IsChecked == true;

    public CollisionResolutionDialog(string fileName)
    {
        InitializeComponent();
        FileNameText.Text = fileName;
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        Choice = CollisionChoice.Replace;
        DialogResult = true;
    }

    private void KeepBoth_Click(object sender, RoutedEventArgs e)
    {
        Choice = CollisionChoice.KeepBoth;
        DialogResult = true;
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        Choice = CollisionChoice.Skip;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Choice = CollisionChoice.Cancel;
        DialogResult = false;
    }
}
