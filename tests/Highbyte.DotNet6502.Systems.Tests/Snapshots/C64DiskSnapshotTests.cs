using System.IO.Compression;
using System.Linq;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Snapshots;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64;
using Highbyte.DotNet6502.Systems.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public class C64DiskSnapshotTests
{
    // Standard single-sided 1541 .d64 image size (683 sectors * 256 bytes).
    private const int D64Size = 174848;

    private static C64 BuildC64()
        => C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL"
        }, NullLoggerFactory.Instance);

    private static DiskDrive1541 GetDrive(C64 c64)
        => c64.IECBus.Devices.OfType<DiskDrive1541>().First();

    private static byte[] BuildTestD64()
    {
        var bytes = new byte[D64Size];
        // Recognizable markers in the data area to verify the bytes survive embedding round-trip.
        bytes[0x1000] = 0xAB;
        bytes[0x2000] = 0xCD;
        bytes[D64Size - 1] = 0xEF;
        return bytes;
    }

    private static void MountDisk(C64 c64, byte[] d64Bytes)
        => GetDrive(c64).SetD64DiskImage(D64Parser.ParseD64File(d64Bytes));

    [Fact]
    public void Saved_package_embeds_mounted_disk_image_bytes()
    {
        var source = BuildC64();
        var d64 = BuildTestD64();
        MountDisk(source, d64);

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        using var archive = new ZipArchive(snapshotStream, ZipArchiveMode.Read);
        var mediaEntry = archive.GetEntry($"{SnapshotService.MediaDirectory}/{C64Disk8SnapshotModule.MediaId}.{C64Disk8SnapshotModule.MediaKind}");
        Assert.NotNull(mediaEntry);

        using var entryStream = mediaEntry!.Open();
        using var ms = new MemoryStream();
        entryStream.CopyTo(ms);
        Assert.Equal(d64, ms.ToArray());
    }

    [Fact]
    public void Round_trip_remounts_disk_into_fresh_machine()
    {
        var source = BuildC64();
        MountDisk(source, BuildTestD64());
        Assert.True(GetDrive(source).IsDisketteInserted);

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildC64();
        Assert.False(GetDrive(restored).IsDisketteInserted); // fresh: no disk
        new SnapshotService().Restore(restored, snapshotStream);

        Assert.True(GetDrive(restored).IsDisketteInserted);
    }

    [Fact]
    public void Round_trip_with_no_disk_leaves_drive_empty()
    {
        var source = BuildC64(); // no disk mounted

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildC64();
        MountDisk(restored, BuildTestD64()); // perturb: target has a disk before restore
        Assert.True(GetDrive(restored).IsDisketteInserted);

        new SnapshotService().Restore(restored, snapshotStream);

        Assert.False(GetDrive(restored).IsDisketteInserted);
    }
}
