using Highbyte.DotNet6502.Systems.Commodore64.Snapshots;
using Highbyte.DotNet6502.Systems.Recording;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Commodore64;

public partial class C64
{
    // The 6510 port itself is CPU model state, captured/restored by the shared cpu-6502
    // snapshot module (v3). This legacy path applies the two raw port bytes from a
    // c64-core v1 payload: both registers set together, so the port's single change
    // notification re-derives the active bank/memory configuration (recomputes
    // CurrentBank and calls Mem.SetMemoryConfiguration) exactly once, with final state.
    internal void RestoreCpuPortState(byte dataDirectionRegister, byte dataRegister)
        => _cpuPort.SetState(dataDirectionRegister, dataRegister);

    // Re-derives the bank/memory configuration from the current CPU port registers and
    // cartridge lines — used by snapshot restore steps that change cartridge lines after
    // the port itself was restored.
    internal void ReapplyMemoryConfigurationFromSnapshot() => ApplyCpuPortMemoryConfiguration();

    // The shared SnapshotService enforces format-version, machine-name, unknown-required-module and
    // module-version rules. The c64-core module additionally validates model/timer-mode on restore.
    public const int SnapshotVersion = 1;
    private readonly IReadOnlyList<ISnapshotModule> _snapshotModules = new ISnapshotModule[]
    {
        new Cpu6502SnapshotModule(),
        new C64CoreSnapshotModule(),
        // c64-vic2 must restore after c64-core so it can re-derive cached display state from the
        // restored IO registers.
        new C64Vic2SnapshotModule(),
        new C64CiaSnapshotModule(),
        new C64InputSnapshotModule(),
        // c64-sid restores the running voices after c64-core has restored the SID register bytes.
        new C64SidSnapshotModule(),
        // c64-disk8 embeds the mounted .d64 image and re-mounts it on restore.
        new C64Disk8SnapshotModule(),
        // c64-cartridge re-attaches an embedded .crt (without reset) and restores live cartridge
        // state. Last, so it re-derives the memory configuration after RAM/CPU-port are restored.
        new C64CartridgeSnapshotModule(),
    };

    public SnapshotMachineId MachineId => new(SystemName, SnapshotVersion);

    public IReadOnlyList<ISnapshotModule> GetSnapshotModules() => _snapshotModules;

    public IRecordingAdapter CreateRecordingAdapter() => new Recording.C64RecordingAdapter(this);

    public SnapshotCompatibility ValidateSnapshot(SnapshotManifest manifest) => SnapshotCompatibility.Compatible();

    // Snapshot restore: re-attach a .crt cartridge without a hard reset, so machine state restored
    // by the other snapshot modules (RAM, CPU, chips) is preserved. The c64-cartridge module
    // restores the cartridge's live state afterwards and re-applies the memory configuration.
    internal void AttachCrtImageForSnapshotRestore(byte[] image, string? sourceName)
        => AttachCrtImageInternal(image, sourceName, reset: false);
}
