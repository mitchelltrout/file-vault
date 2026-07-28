using System.Buffers.Binary;
using FileVault.Service.VaultContainer;

namespace FileVault.Service.Tests.VaultContainer;

public class VaultPrefixTests
{
    private static readonly byte[] FvltBytes = [0x46, 0x56, 0x4C, 0x54];
    private static readonly byte[] FvdtBytes = [0x46, 0x56, 0x44, 0x54];

    [Fact]
    public void DetectBaseOffset_returns_zero_for_undisguised_vault()
    {
        var data = new byte[64];
        FvltBytes.CopyTo(data, 0);
        var ms = new MemoryStream(data);
        Assert.Equal(0, VaultPrefix.DetectBaseOffset(ms));
    }

    [Fact]
    public void DetectBaseOffset_returns_payload_offset_for_disguised_vault()
    {
        var cover = new byte[30];
        var vault = new byte[64];
        FvltBytes.CopyTo(vault, 0);
        var ms = new MemoryStream();
        ms.Write(cover);
        ms.Write(vault);
        WriteTrailer(ms, baseOffset: 30);
        ms.Position = 0;

        Assert.Equal(30, VaultPrefix.DetectBaseOffset(ms));
    }

    [Fact]
    public void DetectBaseOffset_throws_when_trailer_missing()
    {
        var data = new byte[100];
        var ms = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => VaultPrefix.DetectBaseOffset(ms));
    }

    [Fact]
    public void DetectBaseOffset_throws_when_offset_does_not_point_at_FVLT()
    {
        var ms = new MemoryStream();
        ms.Write(new byte[30]);
        ms.Write(new byte[64]);
        WriteTrailer(ms, baseOffset: 30);
        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => VaultPrefix.DetectBaseOffset(ms));
    }

    [Fact]
    public void DetectBaseOffset_throws_for_empty_file()
    {
        var ms = new MemoryStream();
        Assert.Throws<InvalidDataException>(() => VaultPrefix.DetectBaseOffset(ms));
    }

    [Fact]
    public void WriteDisguisedFile_creates_file_with_cover_payload_and_trailer()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            var cover = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
            var payload = new MemoryStream();
            payload.Write(FvltBytes);
            payload.Write(new byte[60]);
            payload.Position = 0;

            VaultPrefix.WriteDisguisedFile(tmp, cover, payload);

            var bytes = File.ReadAllBytes(tmp);
            Assert.Equal(cover.Length + 64 + 16, bytes.Length);
            Assert.Equal(cover, bytes[..4]);
            Assert.Equal(FvltBytes, bytes[4..8]);
            Assert.Equal(FvdtBytes, bytes[(bytes.Length - 16)..(bytes.Length - 12)]);
            var offset = BinaryPrimitives.ReadInt64LittleEndian(bytes[(bytes.Length - 12)..(bytes.Length - 4)]);
            Assert.Equal(4, offset);
            Assert.Equal(FvdtBytes, bytes[(bytes.Length - 4)..]);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact]
    public void WriteDisguisedFile_with_null_cover_writes_undisguised_payload_and_no_trailer()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            var payload = new MemoryStream();
            payload.Write(FvltBytes);
            payload.Write(new byte[60]);
            payload.Position = 0;

            VaultPrefix.WriteDisguisedFile(tmp, null, payload);

            var bytes = File.ReadAllBytes(tmp);
            Assert.Equal(64, bytes.Length);
            Assert.Equal(FvltBytes, bytes[..4]);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact]
    public void RoundTrip_DetectBaseOffset_after_WriteDisguisedFile()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            var cover = new byte[200];
            for (int i = 0; i < 200; i++) cover[i] = (byte)i;
            var payload = new MemoryStream();
            payload.Write(FvltBytes);
            payload.Write(new byte[1000]);
            payload.Position = 0;

            VaultPrefix.WriteDisguisedFile(tmp, cover, payload);

            using var fs = File.OpenRead(tmp);
            Assert.Equal(200, VaultPrefix.DetectBaseOffset(fs));
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    private static void WriteTrailer(MemoryStream ms, long baseOffset)
    {
        ms.Write(FvdtBytes);
        var off = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(off, baseOffset);
        ms.Write(off);
        ms.Write(FvdtBytes);
    }
}
