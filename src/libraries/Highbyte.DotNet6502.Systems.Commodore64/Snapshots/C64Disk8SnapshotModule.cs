using System.Linq;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Commodore64.Snapshots;

/// <summary>
/// Snapshot module for the 1541 disk drive (device 8). Captures whether a disk is mounted and, if
/// so, embeds the mounted <c>.d64</c> image bytes in the package so the snapshot is self-contained
/// and the disk is re-mounted on restore (the user can keep loading from it).
///
/// <para>
/// v1 captures the mounted media and the device number only. The drive's live transfer-protocol
/// state (the receive/send state machines, buffers and timeout counters) is not captured, so a
/// snapshot taken in the middle of an active disk load may not resume that in-flight transfer — the
/// disk is re-mounted, so subsequent loads work. IEC serial-bus line state is likewise not captured.
/// </para>
/// </summary>
public sealed class C64Disk8SnapshotModule : ISnapshotModule
{
    public const string ModuleName = "c64-disk8";
    public const string MediaId = "disk8";
    public const string MediaKind = "d64";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var drive = GetDiskDrive((C64)context.System);
        var inserted = drive?.IsDisketteInserted ?? false;

        writer.WriteBool(inserted);
        writer.WriteInt32(drive?.DeviceNumber ?? 8);

        if (inserted)
        {
            var image = drive!.MountedDiskImage!;
            context.AddEmbeddedMedia(MediaId, MediaKind, image.DiskName, image.RawDiskData);
        }
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var inserted = reader.ReadBool();
        _ = reader.ReadInt32(); // device number (drive is built as device 8; not reassigned in v1)

        var drive = GetDiskDrive((C64)context.System);
        if (drive == null)
        {
            if (inserted)
                context.AddWarning("c64-disk8: snapshot has a mounted disk but the target has no 1541 drive.");
            return;
        }

        if (!inserted)
        {
            drive.RemoveD64DiskImage();
            return;
        }

        if (!context.TryGetEmbeddedMedia(MediaId, out var d64Bytes))
        {
            context.AddWarning("c64-disk8: snapshot marked a disk mounted but no embedded disk image was found.");
            return;
        }

        var diskImage = D64Parser.ParseD64File(d64Bytes);
        drive.SetD64DiskImage(diskImage);
    }

    private static DiskDrive1541? GetDiskDrive(C64 c64)
        => c64.IECBus.Devices.OfType<DiskDrive1541>().FirstOrDefault();
}
