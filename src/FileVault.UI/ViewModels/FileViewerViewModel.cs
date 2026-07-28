using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileVault.UI.Models;

namespace FileVault.UI.ViewModels;

public partial class FileViewerViewModel : ObservableObject
{
    private IReadOnlyList<FileItemModel> _files = [];
    private int _currentIndex;

    [ObservableProperty]
    private FileItemModel? _currentFile;

    [ObservableProperty]
    private bool _canGoPrevious;

    [ObservableProperty]
    private bool _canGoNext;

    public void Open(IReadOnlyList<FileItemModel> files, int startIndex)
    {
        _files = files.Where(f => !f.IsDirectory).ToList();
        _currentIndex = startIndex;
        UpdateCurrent();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous()
    {
        if (_currentIndex > 0) { _currentIndex--; UpdateCurrent(); }
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        if (_currentIndex < _files.Count - 1) { _currentIndex++; UpdateCurrent(); }
    }

    private void UpdateCurrent()
    {
        CurrentFile = _files.Count > 0 ? _files[_currentIndex] : null;
        CanGoPrevious = _currentIndex > 0;
        CanGoNext = _currentIndex < _files.Count - 1;
    }
}
