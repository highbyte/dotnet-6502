using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Oric.Snapshots;

/// <summary>Snapshot module for the Oric's 48 KB RAM and fixed machine wiring.</summary>
public sealed class OricCoreSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "oric-core";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var oric = (Oric)context.System;
        writer.WriteBool(oric.VSyncHackEnabled);
        writer.WriteBytes(oric.SnapshotRam.AsSpan(0, Oric.RamSize).ToArray());
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var oric = (Oric)context.System;
        var capturedVSyncHackEnabled = reader.ReadBool();
        if (capturedVSyncHackEnabled != oric.VSyncHackEnabled)
        {
            context.AddWarning(
                $"oric-core: snapshot VSync modification setting '{capturedVSyncHackEnabled}' differs from target '{oric.VSyncHackEnabled}'.");
        }

        var ram = reader.ReadBytes()
            ?? throw new SnapshotException("oric-core: RAM bytes were missing from the snapshot.");
        if (ram.Length != Oric.RamSize)
        {
            throw new SnapshotException(
                $"oric-core: snapshot RAM size {ram.Length} does not match target {Oric.RamSize}.");
        }

        Array.Copy(ram, oric.SnapshotRam, ram.Length);
    }
}
