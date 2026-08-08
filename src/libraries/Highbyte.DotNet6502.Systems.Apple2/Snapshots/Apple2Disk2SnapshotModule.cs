using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Apple2.Snapshots;

/// <summary>
/// Snapshot module for the Disk II controller in slot 6. Embeds the inserted <c>.dsk</c> image in
/// the package so the snapshot is self-contained, and restores the drive's mechanical and sequencer
/// state: head position, motor, drive select, the Q6/Q7 latches and the read head's position in the
/// current track's nibble stream.
///
/// <para>
/// The head position matters more here than the equivalent does on a C64, because this card has no
/// processor of its own. On a 1541 the drive runs its own DOS and a restored machine can simply ask
/// it for a file again; here the Apple's own CPU is mid-way through stepping phase magnets and
/// shifting bytes out of a latch, so where the head sits <em>is</em> the drive's state. Restoring
/// the disk without it would resume a running RWTS against a head that had jumped elsewhere.
/// </para>
///
/// <para>
/// Not captured: the boot ROM (it comes from the config when the machine is rebuilt) and the
/// nibblized track data (rebuilt from the embedded image on insert, which is why this module
/// restores after <c>apple2-core</c>). Write state needs nothing — the emulated drive is read-only,
/// so no inserted disk can have been modified.
/// </para>
/// </summary>
public sealed class Apple2Disk2SnapshotModule : ISnapshotModule
{
    public const string ModuleName = "apple2-disk2";
    public const string MediaId = "disk1";
    public const string MediaKind = "dsk";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var controller = ((Apple2)context.System).DiskController;
        var rawDiskImageData = controller.SnapshotRawDiskImageData;
        var inserted = controller.IsDiskInserted && rawDiskImageData != null;

        writer.WriteBool(inserted);

        var (halfTrack, motorOn, motorSwitchedOffAtCycle, selectedDrive, q6, q7, nibblePosition) =
            controller.GetSnapshotState();

        writer.WriteInt32(halfTrack);
        writer.WriteBool(motorOn);
        writer.WriteBool(motorSwitchedOffAtCycle.HasValue);
        writer.WriteUInt64(motorSwitchedOffAtCycle ?? 0);
        writer.WriteInt32(selectedDrive);
        writer.WriteBool(q6);
        writer.WriteBool(q7);
        writer.WriteInt32(nibblePosition);

        if (inserted)
            context.AddEmbeddedMedia(MediaId, MediaKind, sourceName: null, rawDiskImageData!);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var controller = ((Apple2)context.System).DiskController;

        var inserted = reader.ReadBool();

        var halfTrack = reader.ReadInt32();
        var motorOn = reader.ReadBool();
        var hasMotorSwitchedOffAtCycle = reader.ReadBool();
        var motorSwitchedOffAtCycle = reader.ReadUInt64();
        var selectedDrive = reader.ReadInt32();
        var q6 = reader.ReadBool();
        var q7 = reader.ReadBool();
        var nibblePosition = reader.ReadInt32();

        // Insert first: nibblizing rebuilds the track data that the head position indexes into.
        if (inserted)
        {
            if (context.TryGetEmbeddedMedia(MediaId, out var diskImageData))
            {
                controller.InsertDiskImage(diskImageData);
            }
            else
            {
                context.AddWarning(
                    "apple2-disk2: snapshot marked a disk inserted but no embedded disk image was found.");
                inserted = false;
            }
        }
        else
        {
            controller.RemoveDiskImage();
        }

        controller.RestoreSnapshotState(
            halfTrack,
            motorOn,
            hasMotorSwitchedOffAtCycle ? motorSwitchedOffAtCycle : null,
            selectedDrive,
            q6,
            q7,
            // With no disk there is no track to be positioned in.
            inserted ? nibblePosition : 0);

        if (inserted && controller.BootRom == null)
            context.AddWarning(
                "apple2-disk2: snapshot has a disk inserted but no Disk II boot ROM is configured, " +
                "so the controller stays invisible to the machine.");
    }
}
