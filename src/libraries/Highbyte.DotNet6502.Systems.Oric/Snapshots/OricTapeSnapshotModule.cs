using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Oric.Snapshots;

/// <summary>Snapshot module for the inserted TAP image and its byte-level transport position.</summary>
public sealed class OricTapeSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "oric-tape";
    public const string MediaId = "tape";
    public const string MediaKind = "tap";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var tape = ((Oric)context.System).Tape;
        var data = tape.SnapshotData;
        writer.WriteBool(data is not null);
        writer.WriteInt32(tape.Position);
        writer.WriteString(tape.SourceName);
        if (data is not null)
            context.AddEmbeddedMedia(MediaId, MediaKind, tape.SourceName, data);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var inserted = reader.ReadBool();
        var position = reader.ReadInt32();
        var sourceName = reader.ReadString();
        var tape = ((Oric)context.System).Tape;

        if (!inserted)
        {
            tape.RestoreSnapshotState(data: null, sourceName: null, position: 0);
            return;
        }

        if (!context.TryGetEmbeddedMedia(MediaId, out var data))
        {
            tape.Eject();
            context.AddWarning("oric-tape: snapshot marked a tape inserted but no embedded TAP image was found.");
            return;
        }

        try
        {
            tape.RestoreSnapshotState(data, sourceName, position);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentOutOfRangeException)
        {
            throw new SnapshotException($"oric-tape: invalid transport state: {exception.Message}");
        }
    }
}
