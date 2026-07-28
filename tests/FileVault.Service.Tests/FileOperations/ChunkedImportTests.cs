using FileVault.Service.FileOperations;
using FileVault.Service.VaultOperations;

namespace FileVault.Service.Tests.FileOperations;

public class ChunkedImportTests
{
    [Fact]
    public async Task Importing_large_file_creates_multiple_chunks()
    {
        var session = TestHelpers.NewInMemoryVault();
        var sourceData = new byte[5_000_000]; // 5 MB -> expect 5 chunks
        Random.Shared.NextBytes(sourceData);
        var src = Path.GetTempFileName();
        File.WriteAllBytes(src, sourceData);

        try
        {
            await ImportOperation.ImportFileAsync(session, "/", src, CollisionBehavior.Replace, CancellationToken.None);

            var node = session.Tree.Find("/" + Path.GetFileName(src));
            Assert.NotNull(node);
            Assert.False(node!.IsDirectory);
            Assert.Equal(5, node.Chunks.Count);
            Assert.Equal(5_000_000, node.Chunks.Sum(c => (long)c.PlaintextLength));
        }
        finally { File.Delete(src); }
    }

    [Fact]
    public async Task Importing_small_file_creates_single_chunk()
    {
        var session = TestHelpers.NewInMemoryVault();
        var src = Path.GetTempFileName();
        File.WriteAllBytes(src, new byte[500_000]);

        try
        {
            await ImportOperation.ImportFileAsync(session, "/", src, CollisionBehavior.Replace, CancellationToken.None);
            var node = session.Tree.Find("/" + Path.GetFileName(src));
            Assert.NotNull(node);
            Assert.Single(node!.Chunks);
            Assert.Equal(500_000, node.Chunks[0].PlaintextLength);
        }
        finally { File.Delete(src); }
    }
}
