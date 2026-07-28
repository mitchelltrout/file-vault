namespace FileVault.Service.VaultFormat;

public static class VaultConstants
{
    public static readonly byte[] Magic = [0x46, 0x56, 0x4C, 0x54]; // "FVLT"
    public const uint FormatVersion = 1;

    // Plaintext header layout (nonce is embedded inside each encrypted blob, not stored separately)
    public const int MagicOffset = 0;           // 4 bytes
    public const int VersionOffset = 4;         // 4 bytes
    public const int SaltOffset = 8;            // 32 bytes
    public const int EncHeaderLenOffset = 40;   // 4 bytes (plaintext length of header block)
    public const int EncHeaderOffset = 44;      // [12 nonce][N ciphertext][16 tag]
}
