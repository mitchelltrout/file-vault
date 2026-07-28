using FileVault.Shared.Ipc.Messages;
using FileVault.UI.Tests.Ipc;
using FileVault.UI.ViewModels;
using FluentAssertions;
using Xunit;

namespace FileVault.UI.Tests.ViewModels;

public class FolderPanelViewModelTests
{
    [Fact]
    public async Task LoadFolders_PopulatesFolderTree()
    {
        var client = new FakeServiceClient();
        client.FolderResponses["/"] = new ListFolderResponse
        {
            Nodes =
            [
                new VfsNodeDto { Name = "Photos", IsDirectory = true },
                new VfsNodeDto { Name = "Videos", IsDirectory = true }
            ]
        };
        var vm = new FolderPanelViewModel(client, "C:/test.vault");
        await vm.LoadAsync("/");
        vm.RootFolders.Should().HaveCount(2);
        vm.RootFolders[0].Name.Should().Be("Photos");
    }

    [Fact]
    public async Task SelectFolder_RaisesEvent()
    {
        var client = new FakeServiceClient();
        var vm = new FolderPanelViewModel(client, "C:/test.vault");
        string? selected = null;
        vm.FolderSelected += path => selected = path;

        await vm.SelectFolderAsync("/Photos");

        selected.Should().Be("/Photos");
    }
}
