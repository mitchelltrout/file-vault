using FileVault.Service.Crypto;
using FileVault.Service.VaultFormat;
using FluentAssertions;

namespace FileVault.Service.Tests.VaultFormat;

public class ContainerHeaderTests
{
    [Fact]
    public void WriteAndRead_RoundTripsPlaintextHeader()
    {
        var stream = new MemoryStream();
        var salt = new byte[32];
        Random.Shared.NextBytes(salt);

        ContainerHeader.WritePlaintext(stream, salt, encryptedHeaderLength: 256);
        stream.Position = 0;
        var header = ContainerHeader.ReadPlaintext(stream);

        header.Salt.Should().Equal(salt);
        header.EncryptedHeaderLength.Should().Be(256);
    }

    [Fact]
    public void ReadPlaintext_ThrowsOnBadMagic()
    {
        var stream = new MemoryStream(new byte[64]);
        var act = () => ContainerHeader.ReadPlaintext(stream);
        act.Should().Throw<InvalidDataException>().WithMessage("*magic*");
    }

    [Fact]
    public void WriteAndReadHeaderBlock_RoundTrips()
    {
        using var key = new VaultKey(new byte[32]);
        var block = new HeaderBlock("My Vault", DateTimeOffset.UtcNow, indexOffset: 512, flags: 0);

        var encrypted = HeaderBlock.Encrypt(key, block);
        var decrypted = HeaderBlock.Decrypt(key, encrypted);

        decrypted.DisplayName.Should().Be("My Vault");
        decrypted.IndexOffset.Should().Be(512);
    }
}
