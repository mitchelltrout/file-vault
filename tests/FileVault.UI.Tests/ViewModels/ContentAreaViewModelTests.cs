using FileVault.Shared.Ipc.Messages;
using FileVault.UI.Tests.Ipc;
using FileVault.UI.ViewModels;
using FluentAssertions;
using Xunit;

namespace FileVault.UI.Tests.ViewModels;

public class ContentAreaViewModelTests
{
    [Fact]
    public async Task LoadFolder_PopulatesItems()
    {
        var client = new FakeServiceClient();
        client.FolderResponses["/Photos"] = new ListFolderResponse
        {
            Nodes =
            [
                new VfsNodeDto { Name = "sunset.jpg", IsDirectory = false, PlaintextLength = 4096 },
                new VfsNodeDto { Name = "trip", IsDirectory = true }
            ]
        };
        var vm = new ContentAreaViewModel(client, "C:/test.vault");
        await vm.LoadFolderAsync("/Photos");
        vm.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteSelectedItems_CallsService()
    {
        var client = new FakeServiceClient();
        client.FolderResponses["/"] = new ListFolderResponse
        {
            Nodes = [new VfsNodeDto { Name = "file.txt", IsDirectory = false }]
        };
        var vm = new ContentAreaViewModel(client, "C:/test.vault");
        await vm.LoadFolderAsync("/");
        vm.SelectedItems.Add(vm.Items[0]);

        await vm.DeleteSelectedAsync();

        client.Calls.Should().Contain(c => c.StartsWith("Delete:"));
    }

    [Fact]
    public void ToggleView_SwitchesBetweenGridAndList()
    {
        var vm = new ContentAreaViewModel(new FakeServiceClient(), "C:/test.vault");
        vm.IsGridView.Should().BeTrue();
        vm.ToggleViewCommand.Execute(null);
        vm.IsGridView.Should().BeFalse();
    }
}
