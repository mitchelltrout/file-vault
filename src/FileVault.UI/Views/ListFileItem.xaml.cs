using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using FileVault.UI.Models;

namespace FileVault.UI.Views;

public partial class ListFileItem : UserControl
{
    public ListFileItem() => InitializeComponent();
}

public class SizeConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not FileItemModel m) return "";
        if (m.IsDirectory) return "";
        return FormatSize(m.PlaintextLength);
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}

public class DateConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dto)
            return dto.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
        return "";
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
