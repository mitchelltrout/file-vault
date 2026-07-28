using FileVault.Service.VaultContainer;

namespace FileVault.Service.Tests.VaultContainer;

public class VaultStreamTests
{
    [Fact]
    public void Position_zero_maps_to_base_offset_in_inner_stream()
    {
        var inner = new MemoryStream(new byte[100]);
        var vs = new VaultStream(inner, baseOffset: 20, leaveOpen: true);
        vs.Position = 0;
        Assert.Equal(20, inner.Position);
    }

    [Fact]
    public void Length_subtracts_base_offset()
    {
        var inner = new MemoryStream(new byte[100]);
        var vs = new VaultStream(inner, baseOffset: 20, leaveOpen: true);
        Assert.Equal(80, vs.Length);
    }

    [Fact]
    public void Read_returns_inner_bytes_starting_at_base_plus_position()
    {
        var data = new byte[100];
        for (int i = 0; i < 100; i++) data[i] = (byte)i;
        var inner = new MemoryStream(data);
        var vs = new VaultStream(inner, baseOffset: 20, leaveOpen: true);
        vs.Position = 5;
        var buf = new byte[3];
        vs.ReadExactly(buf, 0, 3);
        Assert.Equal(new byte[] { 25, 26, 27 }, buf);
    }

    [Fact]
    public void Seek_from_end_subtracts_from_translated_length()
    {
        var inner = new MemoryStream(new byte[100]);
        var vs = new VaultStream(inner, baseOffset: 20, leaveOpen: true);
        vs.Seek(-10, SeekOrigin.End);
        Assert.Equal(70, vs.Position);
        Assert.Equal(90, inner.Position);
    }

    [Fact]
    public void Write_then_read_round_trips_through_offset()
    {
        var inner = new MemoryStream();
        inner.SetLength(100);
        var vs = new VaultStream(inner, baseOffset: 20, leaveOpen: true);
        vs.Position = 10;
        vs.Write(new byte[] { 1, 2, 3 }, 0, 3);
        vs.Position = 10;
        var buf = new byte[3];
        vs.ReadExactly(buf, 0, 3);
        Assert.Equal(new byte[] { 1, 2, 3 }, buf);
    }

    [Fact]
    public void BaseOffset_zero_is_pure_passthrough()
    {
        var inner = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var vs = new VaultStream(inner, baseOffset: 0, leaveOpen: true);
        Assert.Equal(5, vs.Length);
        var buf = new byte[5];
        vs.ReadExactly(buf, 0, 5);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buf);
    }
}
