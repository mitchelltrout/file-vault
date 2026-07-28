using FileVault.Service.VirtualFileSystem;
using FluentAssertions;

namespace FileVault.Service.Tests.VirtualFileSystem;

public class VfsTreeTests
{
    [Fact]
    public void MkDir_CreatesFolder()
    {
        var tree = new VfsTree();
        tree.MkDir("/Photos");
        tree.ListFolder("/").Should().Contain(n => n.Name == "Photos" && n.IsDirectory);
    }

    [Fact]
    public void MkDir_NestedPath_CreatesIntermediates()
    {
        var tree = new VfsTree();
        tree.MkDir("/Photos/2025/Vacation");
        tree.ListFolder("/Photos/2025").Should().Contain(n => n.Name == "Vacation");
    }

    [Fact]
    public void UpsertFile_AddsFileNode()
    {
        var tree = new VfsTree();
        tree.MkDir("/Photos");
        tree.UpsertFile("/Photos/sunset.jpg", dataOffset: 1000, plaintextLength: 5000, encryptedLength: 5028);
        var node = tree.Find("/Photos/sunset.jpg");
        node.Should().NotBeNull();
        node!.DataOffset.Should().Be(1000);
        node.PlaintextLength.Should().Be(5000);
    }

    [Fact]
    public void Delete_RemovesNode()
    {
        var tree = new VfsTree();
        tree.MkDir("/Photos");
        tree.UpsertFile("/Photos/sunset.jpg", 0, 100, 128);
        tree.Delete("/Photos/sunset.jpg");
        tree.Find("/Photos/sunset.jpg").Should().BeNull();
    }

    [Fact]
    public void Delete_Folder_RemovesRecursively()
    {
        var tree = new VfsTree();
        tree.MkDir("/Photos");
        tree.UpsertFile("/Photos/sunset.jpg", 0, 100, 128);
        tree.Delete("/Photos");
        tree.ListFolder("/").Should().NotContain(n => n.Name == "Photos");
    }

    [Fact]
    public void Find_NonExistentPath_ReturnsNull()
    {
        var tree = new VfsTree();
        tree.Find("/Photos/nope.jpg").Should().BeNull();
    }
}
