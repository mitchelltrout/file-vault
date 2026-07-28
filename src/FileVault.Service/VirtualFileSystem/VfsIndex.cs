using MessagePack;
using FileVault.Service.Crypto;

namespace FileVault.Service.VirtualFileSystem;

public static class VfsIndex
{
    public static byte[] Serialize(VfsTree tree) =>
        MessagePackSerializer.Serialize(tree.Root);

    public static VfsTree Deserialize(byte[] bytes)
    {
        var root = MessagePackSerializer.Deserialize<VfsNode>(bytes);
        return new VfsTree(root);
    }

    public static byte[] Encrypt(VaultKey key, VfsTree tree)
    {
        var plaintext = Serialize(tree);
        return AesGcm256.Encrypt(key, plaintext);
    }

    public static VfsTree Decrypt(VaultKey key, byte[] encrypted)
    {
        var plaintext = AesGcm256.Decrypt(key, encrypted);
        return Deserialize(plaintext);
    }
}
