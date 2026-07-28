using FileVault.Service.Crypto;
using FileVault.Service.FileOperations;
using FileVault.Service.VaultOperations;
using FluentAssertions;

namespace FileVault.Service.Tests.FileOperations;

public class ImportExportTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly VaultManager _manager = new();

    public ImportExportTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose()
    {
        _manager.Dispose();
        Directory.Delete(_tempDir, recursive: true);
    }

    private string VaultPath => Path.Combine(_tempDir, "test.vault");
    private async Task<VaultSession> OpenSession()
    {
        await _manager.CreateVaultAsync(VaultPath, "Test", "pass", KeyDerivation.FastParams);
        return await _manager.UnlockAsync(VaultPath, "pass", KeyDerivation.FastParams);
    }

    [Fact]
    public async Task ImportFile_CanBeExported()
    {
        var session = await OpenSession();
        var srcFile = Path.Combine(_tempDir, "hello.txt");
        await File.WriteAllTextAsync(srcFile, "Hello FileVault!");

        await ImportOperation.ImportFileAsync(session, "/", srcFile,
            CollisionBehavior.KeepBoth, CancellationToken.None);

        var exportDir = Path.Combine(_tempDir, "export");
        Directory.CreateDirectory(exportDir);
        await ExportOperation.ExportAsync(session, "/hello.txt", exportDir, CancellationToken.None);

        var result = await File.ReadAllTextAsync(Path.Combine(exportDir, "hello.txt"));
        result.Should().Be("Hello FileVault!");
    }

    [Fact]
    public async Task ImportDirectory_PreservesStructure()
    {
        var session = await OpenSession();
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(Path.Combine(srcDir, "subdir"));
        await File.WriteAllTextAsync(Path.Combine(srcDir, "root.txt"), "root");
        await File.WriteAllTextAsync(Path.Combine(srcDir, "subdir", "nested.txt"), "nested");

        await ImportOperation.ImportDirectoryAsync(session, "/", srcDir,
            CollisionBehavior.Replace, CancellationToken.None);

        session.Tree.Find("/src/subdir/nested.txt").Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteFile_RemovesFromTree()
    {
        var session = await OpenSession();
        var srcFile = Path.Combine(_tempDir, "del.txt");
        await File.WriteAllTextAsync(srcFile, "bye");
        await ImportOperation.ImportFileAsync(session, "/", srcFile,
            CollisionBehavior.Replace, CancellationToken.None);

        DeleteOperation.Delete(session, "/del.txt");
        session.Tree.Find("/del.txt").Should().BeNull();
    }

    [Fact]
    public async Task ImportFile_CollisionKeepBoth_RenamesNewFile()
    {
        var session = await OpenSession();
        var srcFile = Path.Combine(_tempDir, "dup.txt");
        await File.WriteAllTextAsync(srcFile, "original");
        await ImportOperation.ImportFileAsync(session, "/", srcFile,
            CollisionBehavior.Replace, CancellationToken.None);
        await File.WriteAllTextAsync(srcFile, "duplicate");
        await ImportOperation.ImportFileAsync(session, "/", srcFile,
            CollisionBehavior.KeepBoth, CancellationToken.None);

        session.Tree.ListFolder("/").Should().HaveCount(2); // dup.txt and dup (1).txt
    }
}
