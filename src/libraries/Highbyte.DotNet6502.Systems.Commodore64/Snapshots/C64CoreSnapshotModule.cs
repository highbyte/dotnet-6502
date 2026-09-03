using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Commodore64.Snapshots;

/// <summary>
/// Snapshot module for the C64 core memory. Paired with the shared <c>cpu-6502</c> module.
///
/// <para>
/// Captures the full 64 KB <see cref="C64.RAM"/> backing array and the 4 KB <see cref="C64.IO"/>
/// storage array directly (not through the banked <see cref="Memory"/> view). On restore the
/// bytes are copied back into the existing arrays (preserving the memory-map delegates that
/// reference them). Model and timer mode are captured for validation only — the host rebuilds
/// the system from the snapshot's machine variant before restoring.
/// </para>
///
/// <para>
/// v1 additionally carried the raw 6510 CPU port registers; since v2 the port is CPU model
/// state owned by the <c>cpu-6502</c> module (v3). Restoring a v1 payload still reads and
/// applies the two legacy port bytes — in a v1-era file the cpu-6502 module (v2) carries no
/// model state, so exactly one owner exists in either generation.
/// </para>
/// </summary>
public sealed class C64CoreSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "c64-core";

    public string Name => ModuleName;
    public int Version => 2;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var c64 = (C64)context.System;

        // Model (validation/diagnostics only), then a reserved int that carried the CIA timer
        // mode before the CIAs were always advanced per instruction.
        writer.WriteString(c64.Model.Name);
        writer.WriteInt32(0);

        // Backing memory arrays. (v1 wrote the two raw 6510 port bytes here; since v2
        // the port travels as CPU model state in the cpu-6502 module.)
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

        _ = reader.ReadInt32(); // reserved (formerly the CIA timer mode; the CIAs are now always exact)

        // v1 carried the raw 6510 port registers in this position; since v2 the port is
        // CPU model state restored by the cpu-6502 module. For a v1 payload the legacy
        // bytes are still read and applied below (its cpu-6502 module carried no model
        // state, so this is the only port owner in a v1-era file).
        var storedVersion = context.Manifest.Modules.FirstOrDefault(m => m.Name == ModuleName)?.Version ?? 1;
        byte legacyCpuPortDdr = 0;
        byte legacyCpuPortData = 0;
        if (storedVersion < 2)
        {
            legacyCpuPortDdr = reader.ReadByte();
            legacyCpuPortData = reader.ReadByte();
        }

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

        if (storedVersion < 2)
        {
            // Legacy v1 port restore (both registers together); the port's change
            // notification re-derives the active bank/memory configuration.
            c64.RestoreCpuPortState(legacyCpuPortDdr, legacyCpuPortData);
        }
        else
        {
            // v2+: the port was already restored by the cpu-6502 module (which runs
            // first), but the bank/memory configuration must be re-derived against the
            // just-restored RAM/IO and cartridge lines.
            c64.ReapplyMemoryConfigurationFromSnapshot();
        }
    }
}
