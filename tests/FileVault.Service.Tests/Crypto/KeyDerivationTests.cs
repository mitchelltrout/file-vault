using FileVault.Service.Crypto;
using FluentAssertions;

namespace FileVault.Service.Tests.Crypto;

public class KeyDerivationTests
{
    [Fact]
    public void Derive_ProducesDeterministicKey()
    {
        var salt = new byte[32];
        using var key1 = KeyDerivation.Derive("password123", salt, KeyDerivation.FastParams);
        using var key2 = KeyDerivation.Derive("password123", salt, KeyDerivation.FastParams);
        key1.KeyBytes.ToArray().Should().Equal(key2.KeyBytes.ToArray());
    }

    [Fact]
    public void Derive_DifferentPasswordProducesDifferentKey()
    {
        var salt = new byte[32];
        using var key1 = KeyDerivation.Derive("password1", salt, KeyDerivation.FastParams);
        using var key2 = KeyDerivation.Derive("password2", salt, KeyDerivation.FastParams);
        key1.KeyBytes.ToArray().Should().NotEqual(key2.KeyBytes.ToArray());
    }

    [Fact]
    public void Derive_DifferentSaltProducesDifferentKey()
    {
        var salt1 = new byte[32];
        var salt2 = new byte[32];
        salt2[0] = 1;
        using var key1 = KeyDerivation.Derive("password", salt1, KeyDerivation.FastParams);
        using var key2 = KeyDerivation.Derive("password", salt2, KeyDerivation.FastParams);
        key1.KeyBytes.ToArray().Should().NotEqual(key2.KeyBytes.ToArray());
    }

    [Fact]
    public void GenerateSalt_Produces32UniqueBytes()
    {
        var s1 = KeyDerivation.GenerateSalt();
        var s2 = KeyDerivation.GenerateSalt();
        s1.Should().HaveCount(32);
        s1.Should().NotEqual(s2);
    }
}
