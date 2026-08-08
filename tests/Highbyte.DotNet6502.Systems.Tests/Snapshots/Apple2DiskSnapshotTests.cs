using System.IO.Compression;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Disk2;
using Highbyte.DotNet6502.Systems.Apple2.DiskImage;
using Highbyte.DotNet6502.Systems.Apple2.Snapshots;
using Highbyte.DotNet6502.Systems.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public class Apple2DiskSnapshotTests
{
    private static Apple2System BuildApple2()
        => new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);

    private static byte[] BuildTestDiskImage()
    {
        var image = new byte[DskParser.DiskImageSize];
        // Recognizable markers, to prove the bytes survive the embed/extract round trip.
        image[0x1000] = 0xAB;
        image[0x2000] = 0xCD;
        image[^1] = 0xEF;
        return image;
    }

    private static byte[] BuildBootRom()
    {
        var rom = new byte[Disk2Controller.BootRomSize];
        rom[0] = 0xA2; rom[1] = 0x20; rom[2] = 0xA0; rom[3] = 0x00;
        rom[4] = 0xA2; rom[5] = 0x03;
        return rom;
    }

    [Fact]
    public void Saved_package_embeds_the_inserted_disk_image_bytes()
    {
        var source = BuildApple2();
        var image = BuildTestDiskImage();
        source.DiskController.InsertDiskImage(image);

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        using var archive = new ZipArchive(snapshotStream, ZipArchiveMode.Read);
        var mediaEntry = archive.GetEntry(
            $"{SnapshotService.MediaDirectory}/{Apple2Disk2SnapshotModule.MediaId}.{Apple2Disk2SnapshotModule.MediaKind}");
        Assert.NotNull(mediaEntry);

        using var entryStream = mediaEntry!.Open();
        using var ms = new MemoryStream();
        entryStream.CopyTo(ms);
        Assert.Equal(image, ms.ToArray());
    }

    [Fact]
    public void Round_trip_reinserts_the_disk_and_restores_drive_state()
    {
        var source = BuildApple2();
        source.DiskController.SetBootRom(BuildBootRom());
        source.DiskController.InsertDiskImage(BuildTestDiskImage());

        // Drive the card the way the boot ROM does: motor on, read mode, step the head off track 0.
        source.Mem.Read(0xC0E9);                    // motor on
        source.Mem.Read(0xC0EE);                    // Q7 off (read mode)
        source.Mem.Read(0xC0EC);                    // Q6 off
        StepHeadUpOneTrack(source);
        source.Mem.Read(0xC0EC);                    // read a nibble, advancing the stream position

        var sourceTrack = source.DiskController.CurrentTrack;
        Assert.True(sourceTrack > 0);
        Assert.True(source.DiskController.IsMotorOn);
        Assert.True(source.DiskController.DataReadCount > 0);

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildApple2();
        restored.DiskController.SetBootRom(BuildBootRom());
        var result = new SnapshotService().Restore(restored, snapshotStream);

        Assert.Empty(result.Warnings);
        Assert.True(restored.DiskController.IsDiskInserted);
        Assert.True(restored.DiskController.IsEnabled);
        Assert.Equal(sourceTrack, restored.DiskController.CurrentTrack);
        Assert.True(restored.DiskController.IsMotorOn);

        // The read head resumes where it was: the next nibble matches the source machine's.
        Assert.Equal(source.Mem.Read(0xC0EC), restored.Mem.Read(0xC0EC));
    }

    [Fact]
    public void Round_trip_with_an_empty_drive_embeds_no_media_and_stays_empty()
    {
        var source = BuildApple2();
        Assert.False(source.DiskController.IsDiskInserted);

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        using (var archive = new ZipArchive(snapshotStream, ZipArchiveMode.Read, leaveOpen: true))
        {
            Assert.Null(archive.GetEntry(
                $"{SnapshotService.MediaDirectory}/{Apple2Disk2SnapshotModule.MediaId}.{Apple2Disk2SnapshotModule.MediaKind}"));
        }

        // A machine that had a disk must end up empty after restoring an empty-drive snapshot —
        // the snapshot describes the whole machine, not just the parts that changed.
        snapshotStream.Position = 0;
        var restored = BuildApple2();
        restored.DiskController.InsertDiskImage(BuildTestDiskImage());
        new SnapshotService().Restore(restored, snapshotStream);

        Assert.False(restored.DiskController.IsDiskInserted);
    }

    [Fact]
    public void Restoring_an_inserted_disk_without_a_boot_rom_warns_rather_than_failing()
    {
        var source = BuildApple2();
        source.DiskController.SetBootRom(BuildBootRom());
        source.DiskController.InsertDiskImage(BuildTestDiskImage());

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        // Target machine has no disk2 ROM configured, so the controller cannot appear on the bus.
        snapshotStream.Position = 0;
        var restored = BuildApple2();
        var result = new SnapshotService().Restore(restored, snapshotStream);

        Assert.True(restored.DiskController.IsDiskInserted);
        Assert.False(restored.DiskController.IsEnabled);
        Assert.Contains(result.Warnings, w => w.Contains("boot ROM", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Pulses the stepper phases to move the head from track 0 to track 1 (two half-track steps).
    /// Phase alignment repeats every four half-tracks, so the pulses have to walk in order.
    /// </summary>
    private static void StepHeadUpOneTrack(Apple2System apple2)
    {
        for (var halfStep = 0; halfStep < 2; halfStep++)
        {
            var phase = (apple2.DiskController.CurrentTrack * 2 + halfStep + 1) & 0x03;
            apple2.Mem.Read((ushort)(Disk2Controller.IoBaseAddress + phase * 2 + 1));   // phase on
            apple2.Mem.Read((ushort)(Disk2Controller.IoBaseAddress + phase * 2));       // phase off
        }
    }
}
