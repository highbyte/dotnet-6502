using Highbyte.DotNet6502.Systems.Apple2.Disk2;
using Highbyte.DotNet6502.Systems.Apple2.DiskImage;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Writing to the emulated drive: the soft-switch write path, the nibble stream going back to
/// sectors, and the write-protect notch.
/// </summary>
public class Disk2WriteTests
{
    private ulong _cycles;

    private static byte[] BuildDiskImage(byte seed = 31)
    {
        var image = new byte[DskParser.DiskImageSize];
        for (var i = 0; i < image.Length; i++)
            image[i] = (byte)(i * seed);
        return image;
    }

    private static byte[] BuildBootRom()
    {
        var rom = new byte[Disk2Controller.BootRomSize];
        rom[0] = 0xA2; rom[1] = 0x20; rom[2] = 0xA0; rom[3] = 0x00;
        rom[4] = 0xA2; rom[5] = 0x03; rom[6] = 0x86; rom[7] = 0x3C;
        return rom;
    }

    private Disk2Controller CreateDrive(byte[] image, bool writeProtected = false)
    {
        var controller = new Disk2Controller(() => _cycles);
        controller.SetBootRom(BuildBootRom());
        controller.InsertDiskImage(image);
        controller.SetWriteProtected(writeProtected);
        controller.BusAccess(0xC0E9);   // motor on
        return controller;
    }

    /// <summary>
    /// One byte down the wire the way RWTS does it: store to Q6H to load the data register, then
    /// touch Q6L to shift it out, a byte time apart.
    /// </summary>
    private void WriteByte(Disk2Controller controller, byte value)
    {
        _cycles += Disk2Controller.CyclesPerNibble;
        controller.BusAccess(0xC0ED, isRead: false, value: value);
        controller.BusAccess(0xC0EC);
    }

    private void WriteTrack(Disk2Controller controller, ReadOnlySpan<byte> nibbles)
    {
        controller.BusAccess(0xC0EF);   // Q7H: write mode
        foreach (var b in nibbles)
            WriteByte(controller, b);
        controller.BusAccess(0xC0EE);   // Q7L: back to read mode, which decodes what was written
    }

    // ------------------------------------------------------------------ codec

    [Fact]
    public void A_Decoded_Sector_Round_Trips_Its_Encoding()
    {
        var sector = new byte[Disk2NibbleCodec.SectorSize];
        for (var i = 0; i < sector.Length; i++)
            sector[i] = (byte)(i * 7 + 3);

        Span<byte> encoded = stackalloc byte[Disk2NibbleCodec.EncodedDataSize];
        Disk2NibbleCodec.EncodeSector(sector, encoded);

        var decoded = new byte[Disk2NibbleCodec.SectorSize];
        Assert.True(Disk2NibbleCodec.TryDecodeSector(encoded, decoded));
        Assert.Equal(sector, decoded);
    }

    [Fact]
    public void A_Sector_With_A_Corrupt_Byte_Or_Checksum_Fails_To_Decode()
    {
        var sector = new byte[Disk2NibbleCodec.SectorSize];
        var encoded = new byte[Disk2NibbleCodec.EncodedDataSize];
        Disk2NibbleCodec.EncodeSector(sector, encoded);
        var decoded = new byte[Disk2NibbleCodec.SectorSize];

        // $D5 is deliberately not a legal data byte - it is reserved for field prologs.
        var corruptByte = (byte[])encoded.Clone();
        corruptByte[100] = 0xD5;
        Assert.False(Disk2NibbleCodec.TryDecodeSector(corruptByte, decoded));

        // A legal byte in the wrong place still breaks the running checksum.
        var corruptChecksum = (byte[])encoded.Clone();
        corruptChecksum[^1] = corruptChecksum[^1] == 0x96 ? (byte)0x97 : (byte)0x96;
        Assert.False(Disk2NibbleCodec.TryDecodeSector(corruptChecksum, decoded));
    }

    // ------------------------------------------------------- track -> image

    [Fact]
    public void A_Nibblized_Track_Decodes_Back_To_The_Sectors_It_Came_From()
    {
        var image = BuildDiskImage();
        var track = Disk2TrackNibblizer.BuildNibbleTrack(image, 17);

        var target = new byte[DskParser.DiskImageSize];
        var recovered = Disk2TrackNibblizer.ApplyNibbleTrackToImage(track, 17, target);

        Assert.Equal(DskParser.SectorsPerTrack, recovered);
        for (var sector = 0; sector < DskParser.SectorsPerTrack; sector++)
        {
            var offset = DskParser.SectorOffset(17, sector);
            Assert.Equal(
                image.AsSpan(offset, Disk2NibbleCodec.SectorSize).ToArray(),
                target.AsSpan(offset, Disk2NibbleCodec.SectorSize).ToArray());
        }
    }

    [Fact]
    public void A_Field_Straddling_The_End_Of_The_Track_Is_Still_Recovered()
    {
        var image = BuildDiskImage();
        var track = Disk2TrackNibblizer.BuildNibbleTrack(image, 3);

        // Rotate so a sector's fields wrap the buffer end - a disk has no "end", and a rewritten
        // sector does not have to land where the original did.
        var rotated = new byte[track.Length];
        const int Shift = 200;
        for (var i = 0; i < track.Length; i++)
            rotated[i] = track[(i + Shift) % track.Length];

        var target = new byte[DskParser.DiskImageSize];
        var recovered = Disk2TrackNibblizer.ApplyNibbleTrackToImage(rotated, 3, target);

        Assert.Equal(DskParser.SectorsPerTrack, recovered);
    }

    [Fact]
    public void Garbage_Is_Skipped_Rather_Than_Written_To_The_Image()
    {
        var target = BuildDiskImage(seed: 11);
        var before = (byte[])target.Clone();
        var garbage = new byte[Disk2TrackNibblizer.TrackSize];
        Array.Fill(garbage, (byte)0xFF);

        var recovered = Disk2TrackNibblizer.ApplyNibbleTrackToImage(garbage, 0, target);

        Assert.Equal(0, recovered);
        Assert.Equal(before, target);
    }

    // ---------------------------------------------------- the drive itself

    [Fact]
    public void Writing_A_Track_Puts_Its_Sectors_Into_The_Disk_Image()
    {
        var original = BuildDiskImage(seed: 31);
        var controller = CreateDrive(original);

        // What the machine will lay down: the same disk with different content on track 0.
        var modified = BuildDiskImage(seed: 97);
        var newTrack = Disk2TrackNibblizer.BuildNibbleTrack(modified, 0);

        byte[]? handed = null;
        controller.DiskImageWritten += image => handed = image;

        WriteTrack(controller, newTrack);

        Assert.True(controller.HasUnsavedChanges);
        Assert.NotNull(handed);
        Assert.True(controller.DataWriteCount > 0);

        // Track 0's sectors now hold the new content...
        for (var sector = 0; sector < DskParser.SectorsPerTrack; sector++)
        {
            var offset = DskParser.SectorOffset(0, sector);
            Assert.Equal(
                modified.AsSpan(offset, Disk2NibbleCodec.SectorSize).ToArray(),
                handed!.AsSpan(offset, Disk2NibbleCodec.SectorSize).ToArray());
        }

        // ...and no other track was touched.
        var untouched = DskParser.SectorOffset(1, 0);
        Assert.Equal(
            original.AsSpan(untouched, Disk2NibbleCodec.SectorSize).ToArray(),
            handed!.AsSpan(untouched, Disk2NibbleCodec.SectorSize).ToArray());
    }

    [Fact]
    public void The_Callers_Image_Array_Is_Never_Mutated()
    {
        var original = BuildDiskImage(seed: 31);
        var callerCopy = (byte[])original.Clone();
        var controller = CreateDrive(original);

        WriteTrack(controller, Disk2TrackNibblizer.BuildNibbleTrack(BuildDiskImage(seed: 97), 0));

        // The array handed to InsertDiskImage may be a snapshot's bytes or a download cache entry.
        Assert.Equal(callerCopy, original);
    }

    [Fact]
    public void A_Write_Protected_Disk_Ignores_Writes()
    {
        var original = BuildDiskImage(seed: 31);
        var controller = CreateDrive(original, writeProtected: true);

        var raised = false;
        controller.DiskImageWritten += _ => raised = true;

        WriteTrack(controller, Disk2TrackNibblizer.BuildNibbleTrack(BuildDiskImage(seed: 97), 0));

        Assert.Equal(0UL, controller.DataWriteCount);
        Assert.False(controller.HasUnsavedChanges);
        Assert.False(raised);
    }

    [Fact]
    public void Writing_Faster_Than_The_Disk_Turns_Drops_The_Early_Byte()
    {
        var controller = CreateDrive(BuildDiskImage());
        controller.BusAccess(0xC0EF);   // write mode

        _cycles += Disk2Controller.CyclesPerNibble;
        controller.BusAccess(0xC0ED, isRead: false, value: 0xD5);
        controller.BusAccess(0xC0EC);
        Assert.Equal(1UL, controller.DataWriteCount);

        // Same byte time: the surface has not moved, so this store cannot have been laid down.
        controller.BusAccess(0xC0ED, isRead: false, value: 0xAA);
        controller.BusAccess(0xC0EC);
        Assert.Equal(1UL, controller.DataWriteCount);
    }

    [Fact]
    public void Reading_Back_A_Written_Byte_Gives_What_Was_Written()
    {
        var controller = CreateDrive(BuildDiskImage());

        controller.BusAccess(0xC0EF);
        WriteByte(controller, 0xD5);
        controller.BusAccess(0xC0EE);   // back to read mode

        // A full revolution later the same spot comes round again.
        _cycles += Disk2Controller.CyclesPerNibble * (ulong)Disk2TrackNibblizer.TrackSize;
        Assert.Equal(0xD5, controller.BusAccess(0xC0EC));
    }
}
