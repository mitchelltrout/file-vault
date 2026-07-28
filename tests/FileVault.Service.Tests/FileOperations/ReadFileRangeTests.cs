using FileVault.Service.FileOperations;
using FileVault.Service.VaultOperations;

namespace FileVault.Service.Tests.FileOperations;

public class ReadFileRangeTests
{
    private static (VaultSession session, string path, byte[] expected) Setup(int totalBytes)
    {
        var session = TestHelpers.NewInMemoryVault();
        var data = new byte[totalBytes];
        for (int i = 0; i < totalBytes; i++) data[i] = (byte)(i & 0xFF);

        var src = Path.GetTempFileName();
        File.WriteAllBytes(src, data);
        ImportOperation.ImportFileAsync(session, "/", src, CollisionBehavior.Replace, CancellationToken.None)
            .GetAwaiter().GetResult();
        File.Delete(src);
        return (session, "/" + Path.GetFileName(src), data);
    }

    [Fact]
    public void Range_inside_single_chunk()
    {
        var (session, path, data) = Setup(2_000_000); // 2 chunks
        var actual = ReadFileRangeOperation.Read(session, path, offset: 100, length: 50);
        Assert.Equal(data[100..150], actual);
    }

    [Fact]
    public void Range_spanning_two_chunks()
    {
        var (session, path, data) = Setup(2_000_000);
        // Chunk boundary at 1 MB. Range straddles it.
        var actual = ReadFileRangeOperation.Read(session, path, offset: 1_000_000 - 50, length: 100);
        Assert.Equal(data[(1_000_000 - 50)..(1_000_000 + 50)], actual);
    }

    [Fact]
    public void Range_spanning_many_chunks()
    {
        var (session, path, data) = Setup(5_000_000);
        // Request 1.5 MB starting at offset 100 to span multiple 1 MB chunks without hitting the 2 MB cap.
        var actual = ReadFileRangeOperation.Read(session, path, offset: 100, length: 1_500_000);
        Assert.Equal(data[100..1_500_100], actual);
    }

    [Fact]
    public void Range_at_file_start()
    {
        var (session, path, data) = Setup(2_000_000);
        var actual = ReadFileRangeOperation.Read(session, path, offset: 0, length: 100);
        Assert.Equal(data[..100], actual);
    }

    [Fact]
    public void Range_at_file_end_clamps()
    {
        var (session, path, data) = Setup(2_000_000);
        var actual = ReadFileRangeOperation.Read(session, path, offset: 1_999_900, length: 1000);
        Assert.Equal(data[1_999_900..], actual);
        Assert.Equal(100, actual.Length);
    }

    [Fact]
    public void Offset_past_eof_returns_empty()
    {
        var (session, path, _) = Setup(1_000_000);
        var actual = ReadFileRangeOperation.Read(session, path, offset: 5_000_000, length: 100);
        Assert.Empty(actual);
    }

    [Fact]
    public void Length_capped_at_2MB()
    {
        var (session, path, _) = Setup(10_000_000);
        var actual = ReadFileRangeOperation.Read(session, path, offset: 0, length: 100_000_000);
        Assert.Equal(2 * 1024 * 1024, actual.Length);
    }
}
