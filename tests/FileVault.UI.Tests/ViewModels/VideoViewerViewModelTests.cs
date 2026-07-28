using FileVault.UI.ViewModels;
using Xunit;

namespace FileVault.UI.Tests.ViewModels;

public class VideoViewerViewModelTests
{
    [Fact]
    public void Dispose_releases_player_and_libvlc()
    {
        var vm = new VideoViewerViewModel();
        vm.Dispose(); // should not throw even if never opened
    }
}
