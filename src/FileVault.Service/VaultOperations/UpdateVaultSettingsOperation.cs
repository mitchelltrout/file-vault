using FileVault.Service.VaultContainer;
using FileVault.Service.VaultFormat;

namespace FileVault.Service.VaultOperations;

public static class UpdateVaultSettingsOperation
{
    /// <summary>
    /// Rewrites the vault file to apply a new cover image (or remove disguise if empty).
    /// May rename the file's extension between .jpg and .vault depending on whether a cover
    /// image is set.
    /// </summary>
    /// <returns>The new vault file path.</returns>
    public static string Apply(VaultSession session, byte[] newCoverImageBytes)
    {
        // 1. Read current header, update cover fields.
        var header = VaultContainerIo.ReadHeaderBlock(session.Stream, session.Key);
        header.CoverImageBytes = newCoverImageBytes;
        header.CoverImageHash = newCoverImageBytes.Length > 0
            ? System.Security.Cryptography.SHA256.HashData(newCoverImageBytes)
            : [];

        // 2. Rebuild the vault payload from scratch (header size changes with cover image).
        var plaintext = ContainerHeader.ReadPlaintext(session.Stream);
        var salt = plaintext.Salt;
        var payload = new MemoryStream();
        VaultContainerIo.WriteNewVault(payload, session.Key, salt, header, session.Tree);

        // 3. Determine new file path (extension swap).
        var oldPath = session.VaultPath;
        var newPath = newCoverImageBytes.Length > 0
            ? Path.ChangeExtension(oldPath, ".jpg")
            : Path.ChangeExtension(oldPath, ".vault");

        // 4. Close current stream so we can replace the file.
        session.CloseStream();

        // 5. Write the new disguised/undisguised file.
        VaultPrefix.WriteDisguisedFile(
            newPath,
            newCoverImageBytes.Length > 0 ? newCoverImageBytes : null,
            payload);

        // 6. If extension changed, delete the old path.
        if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath))
            File.Delete(oldPath);

        // 7. Reopen so the session points at the new file with a fresh wrapped stream.
        session.ReopenAt(newPath);

        return newPath;
    }
}
