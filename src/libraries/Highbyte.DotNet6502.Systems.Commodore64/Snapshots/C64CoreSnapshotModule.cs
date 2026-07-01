using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Commodore64.Snapshots;

/// <summary>
/// Snapshot module for the C64 core memory and 6510 CPU port. Paired with the shared
/// <c>cpu-6502</c> module. Step 4 of the snapshot feature: enough to restore a BASIC-ready state
/// with no media and without per-chip (CIA/VIC-II/SID) state — those are separate modules.
///
/// <para>
/// Captures the full 64 KB <see cref="C64.RAM"/> backing array and the 4 KB <see cref="C64.IO"/>
/// storage array directly (not through the banked <see cref="Memory"/> view), plus the raw 6510
/// CPU port data-direction/data registers that drive bank switching. On restore the bytes are
/// copied back into the existing arrays (preserving the memory-map delegates that reference them),
/// and the CPU port registers are re-applied so <see cref="C64.CurrentBank"/> and the active memory
/// configuration are recomputed. Model and timer mode are captured for validation only — the host
/// rebuilds the system from the snapshot's machine variant before restoring.
/// </para>
/// </summary>
public sealed class C64CoreSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "c64-core";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var c64 = (C64)context.System;

        // Model / timer mode (validation/diagnostics only).
        writer.WriteString(c64.Model.Name);
        writer.WriteInt32((int)c64.TimerMode);

        // 6510 CPU port raw registers (drive bank switching).
        writer.WriteByte(c64.SnapshotCpuPortDataDirectionRegister);
        writer.WriteByte(c64.SnapshotCpuPortDataRegister);

        // Backing memory arrays.
        writer.WriteBytes(c64.RAM);
        writer.WriteBytes(c64.IO);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var c64 = (C64)context.System;

        var capturedModel = reader.ReadString();
        if (!string.Equals(capturedModel, c64.Model.Name, StringComparison.Ordinal))
            context.AddWarning(
                $"c64-core: snapshot model '{capturedModel}' differs from target model '{c64.Model.Name}'.");

        var capturedTimerMode = (TimerMode)reader.ReadInt32();
        if (capturedTimerMode != c64.TimerMode)
            context.AddWarning(
                $"c64-core: snapshot timer mode '{capturedTimerMode}' differs from target '{c64.TimerMode}'.");

        var cpuPortDdr = reader.ReadByte();
        var cpuPortData = reader.ReadByte();

        var ram = reader.ReadBytes() ?? throw new SnapshotException("c64-core: RAM bytes were missing.");
        var io = reader.ReadBytes() ?? throw new SnapshotException("c64-core: IO bytes were missing.");

        if (ram.Length != c64.RAM.Length)
            throw new SnapshotException(
                $"c64-core: snapshot RAM size {ram.Length} does not match target {c64.RAM.Length}.");
        if (io.Length != c64.IO.Length)
            throw new SnapshotException(
                $"c64-core: snapshot IO size {io.Length} does not match target {c64.IO.Length}.");

        // Copy into the existing arrays so the memory-map delegates (which close over these exact
        // array instances) keep working. Reassigning C64.RAM/IO would leave the map pointing at the
        // old arrays.
        Array.Copy(ram, c64.RAM, ram.Length);
        Array.Copy(io, c64.IO, io.Length);

        // Restore the CPU port and re-derive the active bank/memory configuration.
        c64.SnapshotCpuPortDataDirectionRegister = cpuPortDdr;
        c64.SnapshotCpuPortDataRegister = cpuPortData;
        c64.ApplyCpuPortMemoryConfigurationFromSnapshot();
    }
}
