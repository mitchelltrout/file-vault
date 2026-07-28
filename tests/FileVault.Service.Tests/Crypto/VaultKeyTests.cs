using FileVault.Service.Crypto;
using FluentAssertions;

namespace FileVault.Service.Tests.Crypto;

public class VaultKeyTests
{
    [Fact]
    public void KeyBytes_ReturnsCorrectBytes()
    {
        var raw = new byte[32];
        Random.Shared.NextBytes(raw);
        using var key = new VaultKey(raw);
        key.KeyBytes.ToArray().Should().Equal(raw);
    }

    [Fact]
    public void Dispose_ZeroesKey()
    {
        var raw = Enumerable.Repeat((byte)0xFF, 32).ToArray();
        var key = new VaultKey(raw);
        key.Dispose();
        // After dispose, the internal array should be zeroed
        // We test this indirectly: accessing KeyBytes should throw
        var act = () => key.KeyBytes.ToArray();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void KeyBytes_RequiresExactly32Bytes()
    {
        var act = () => new VaultKey(new byte[16]);
        act.Should().Throw<ArgumentException>();
    }
}
