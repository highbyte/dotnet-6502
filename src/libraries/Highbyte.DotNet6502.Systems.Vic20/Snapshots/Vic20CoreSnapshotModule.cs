using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Vic20.Snapshots;

/// <summary>
/// Snapshot module for the VIC-20 core memory: the low RAM ($0000-$03FF), main RAM
/// ($1000-$1FFF), VIC-I register storage ($9000-$900F) and color RAM ($9400-$97FF).
///
/// <para>
/// Unlike the C64's VIC-II, the VIC-20's VIC-I registers and color RAM are plain RAM-mapped
/// arrays (the rasterizer reads them live from memory), so restoring the bytes restores the full
/// video state — there is no cached display pointer to re-derive. The unexpanded VIC-20 has no bank
/// switching, so there is no CPU-port/memory-configuration state either. Bytes are copied into the
/// existing backing arrays (which Memory maps by reference), not reassigned.
/// </para>
/// </summary>
public sealed class Vic20CoreSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "vic20-core";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var vic20 = (Vic20)context.System;
        writer.WriteBytes(vic20.SnapshotLowRam);
        writer.WriteBytes(vic20.SnapshotMainRam);
        writer.WriteBytes(vic20.SnapshotVicRegisterStorage);
        writer.WriteBytes(vic20.SnapshotColorRam);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var vic20 = (Vic20)context.System;
        RestoreArray(reader, vic20.SnapshotLowRam, "low RAM");
        RestoreArray(reader, vic20.SnapshotMainRam, "main RAM");
        RestoreArray(reader, vic20.SnapshotVicRegisterStorage, "VIC-I register storage");
        RestoreArray(reader, vic20.SnapshotColorRam, "color RAM");
    }

    private static void RestoreArray(SnapshotModuleReader reader, byte[] target, string name)
    {
        var bytes = reader.ReadBytes()
            ?? throw new SnapshotException($"vic20-core: {name} bytes were missing from the snapshot.");
        if (bytes.Length != target.Length)
            throw new SnapshotException(
                $"vic20-core: snapshot {name} size {bytes.Length} does not match target {target.Length}.");
        Array.Copy(bytes, target, bytes.Length);
    }
}
