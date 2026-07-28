using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FileVault.UI.ViewModels;

namespace FileVault.UI.Dialogs;

public partial class PasswordGeneratorControl : UserControl
{
    public PasswordDialogViewModel? ViewModel { get; private set; }
    public event Action<string>? PasswordSelected;

    public PasswordGeneratorControl() => InitializeComponent();

    public void SetViewModel(PasswordDialogViewModel vm)
    {
        ViewModel = vm;
    }

    private void ModeChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var isRandom = RandomModeBtn.IsChecked == true;
        ViewModel.GeneratorMode = isRandom ? GeneratorMode.Random : GeneratorMode.Memorable;
        if (RandomOptions is not null)
            RandomOptions.Visibility = isRandom ? Visibility.Visible : Visibility.Collapsed;
        if (MemorableOptions is not null)
            MemorableOptions.Visibility = isRandom ? Visibility.Collapsed : Visibility.Visible;
    }

    private void LengthSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ViewModel is null) return;
        ViewModel.RandomLength = (int)e.NewValue;
        if (LengthLabel is not null)
            LengthLabel.Text = ViewModel.RandomLength.ToString();
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.IncludeUppercase = UpperCheck.IsChecked == true;
        ViewModel.IncludeLowercase = LowerCheck.IsChecked == true;
        ViewModel.IncludeNumbers = NumCheck.IsChecked == true;
        ViewModel.IncludeSymbols = SymCheck.IsChecked == true;
        ViewModel.MemorableSeparator = SeparatorBox.Text;
        ViewModel.GenerateCommand.Execute(null);
        GeneratedBox.Text = ViewModel.GeneratedPassword;
        EntropyLabel.Text = ViewModel.EntropyDescription;
    }

    private void UsePassword_Click(object sender, RoutedEventArgs e)
    {
        PasswordSelected?.Invoke(GeneratedBox.Text);
    }
}
