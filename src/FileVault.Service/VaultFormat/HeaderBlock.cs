using MessagePack;
using FileVault.Service.Crypto;

namespace FileVault.Service.VaultFormat;

[MessagePackObject]
public class HeaderBlock
{
    [Key(0)] public string DisplayName { get; set; } = "";
    [Key(1)] public long CreatedAtUtc { get; set; }
    [Key(2)] public long IndexOffset { get; set; }
    [Key(3)] public uint Flags { get; set; }
    [Key(4)] public byte[] CoverImageBytes { get; set; } = [];
    [Key(5)] public byte[] CoverImageHash { get; set; } = [];

    public HeaderBlock() { }

    public HeaderBlock(string displayName, DateTimeOffset createdAt, long indexOffset, uint flags)
    {
        DisplayName = displayName;
        CreatedAtUtc = createdAt.ToUnixTimeSeconds();
        IndexOffset = indexOffset;
        Flags = flags;
    }

    public static byte[] Encrypt(VaultKey key, HeaderBlock block)
    {
        var plaintext = MessagePackSerializer.Serialize(block);
        return AesGcm256.Encrypt(key, plaintext);
    }

    public static HeaderBlock Decrypt(VaultKey key, byte[] encrypted)
    {
        var plaintext = AesGcm256.Decrypt(key, encrypted);
        return MessagePackSerializer.Deserialize<HeaderBlock>(plaintext);
    }
}
