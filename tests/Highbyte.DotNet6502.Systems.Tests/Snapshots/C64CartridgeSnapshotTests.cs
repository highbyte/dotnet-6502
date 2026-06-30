using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.Crt;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Snapshots;
using Highbyte.DotNet6502.Systems.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public class C64CartridgeSnapshotTests
{
    private static C64 BuildC64()
        => C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL"
        }, NullLoggerFactory.Instance);

    // Builds a minimal 4-bank Magic Desk .crt where each 8K bank is filled with a distinct byte
    // (0x10 + bank), so the active bank is observable by reading $8000 (ROML).
    private static byte[] BuildMagicDeskCrt()
    {
        const int headerLength = 0x40;
        const int bankCount = 4;
        const int bankSize = 0x2000;
        var size = headerLength + bankCount * (0x10 + bankSize);
        var bytes = new byte[size];

        Encoding.ASCII.GetBytes("C64 CARTRIDGE   ").CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0x10, 4), headerLength);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(0x14, 2), 0x0100);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(0x16, 2), (ushort)C64CrtHardwareType.MagicDesk);
        bytes[0x18] = 0; // EXROM low
        bytes[0x19] = 1; // GAME high
        Encoding.ASCII.GetBytes("MAGIC DESK TEST").CopyTo(bytes, 0x20);

        var offset = headerLength;
        for (ushort bank = 0; bank < bankCount; bank++)
        {
            Encoding.ASCII.GetBytes("CHIP").CopyTo(bytes, offset);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset + 4, 4), (uint)(0x10 + bankSize));
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset + 8, 2), 0); // ROM
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset + 10, 2), bank);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset + 12, 2), 0x8000);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset + 14, 2), bankSize);
            var data = bytes.AsSpan(offset + 0x10, bankSize);
            data.Fill((byte)(0x10 + bank));
            offset += 0x10 + bankSize;
        }
        return bytes;
    }

    [Fact]
    public void Round_trip_reattaches_cartridge_and_restores_active_bank()
    {
        var source = BuildC64();
        var crt = BuildMagicDeskCrt();
        source.AttachCrtImage(crt, "magic-desk.crt");
        // Switch to bank 2 (write Magic Desk IO1 register), so ROML shows 0x12.
        source.Mem.Write(0xDE00, 2);
        Assert.Equal(0x12, source.Mem.Read(0x8000));

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildC64(); // fresh: no cartridge
        Assert.Null(restored.CartridgeSlot.AttachedCartridge);
        new SnapshotService().Restore(restored, snapshotStream);

        // Cartridge re-attached and the active bank (live register state) restored.
        Assert.NotNull(restored.CartridgeSlot.AttachedCartridge);
        Assert.Equal(0x12, restored.Mem.Read(0x8000));

        // And bank switching still works on the restored cartridge.
        restored.Mem.Write(0xDE00, 1);
        Assert.Equal(0x11, restored.Mem.Read(0x8000));
    }

    [Fact]
    public void Saved_package_embeds_crt_image_bytes()
    {
        var source = BuildC64();
        var crt = BuildMagicDeskCrt();
        source.AttachCrtImage(crt, "magic-desk.crt");

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        using var archive = new ZipArchive(snapshotStream, ZipArchiveMode.Read);
        var mediaEntry = archive.GetEntry($"{SnapshotService.MediaDirectory}/{C64CartridgeSnapshotModule.MediaId}.{C64CartridgeSnapshotModule.MediaKind}");
        Assert.NotNull(mediaEntry);

        using var entryStream = mediaEntry!.Open();
        using var ms = new MemoryStream();
        entryStream.CopyTo(ms);
        Assert.Equal(crt, ms.ToArray());
    }

    [Fact]
    public void Round_trip_with_no_cartridge_leaves_slot_empty()
    {
        var source = BuildC64(); // no cartridge

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildC64();
        new SnapshotService().Restore(restored, snapshotStream);

        Assert.Null(restored.CartridgeSlot.AttachedCartridge);
    }
}
