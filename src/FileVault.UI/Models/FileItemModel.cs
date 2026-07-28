using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileVault.UI.Models;

public partial class FileItemModel : ObservableObject
{
    public string Name { get; init; } = "";
    public bool IsDirectory { get; init; }
    public long PlaintextLength { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }
    public string VaultPath { get; init; } = ""; // full path inside the vault

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    [NotifyPropertyChangedFor(nameof(ShowGlyph))]
    private BitmapImage? _thumbnail;

    [ObservableProperty]
    private bool _isImage;

    [ObservableProperty]
    private bool _isVideo;

    [ObservableProperty]
    private int _rotationDegrees;

    public bool HasThumbnail => Thumbnail is not null;
    public bool ShowGlyph => Thumbnail is null;

    public string Glyph => IsDirectory ? "\uED41"      // folder
                         : IsVideo     ? "\uE714"      // video
                         : IsImage     ? "\uEB9F"      // picture
                                       : "\uE7C3";     // document

    public static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
    };

    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".webm", ".avi", ".m4v"
    };

    public string Extension => System.IO.Path.GetExtension(Name);
}
