using System.IO;

namespace FileVault.UI.Dialogs;

internal static class CoverImageValidator
{
    public const int MaxBytes = 8 * 1024 * 1024;

    public static void Validate(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 4)
            throw new InvalidDataException("Cover image is empty.");
        if (bytes[0] != 0xFF || bytes[1] != 0xD8)
            throw new InvalidDataException("Cover image must be a JPEG file.");
        if (bytes[^2] != 0xFF || bytes[^1] != 0xD9)
            throw new InvalidDataException(
                "Cover image is malformed or has trailing data — please re-save it from an image editor and try again.");
        if (bytes.Length > MaxBytes)
            throw new InvalidDataException("Cover image is too large (max 8 MB).");
    }
}
