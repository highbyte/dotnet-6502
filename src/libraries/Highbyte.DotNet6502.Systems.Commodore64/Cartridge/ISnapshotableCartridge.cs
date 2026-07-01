namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// Optional capability for a cartridge to capture/restore its live mutable state — bank/control
/// registers, writable RAM, freeze flags, etc. — for emulator state snapshots.
///
/// <para>
/// The read-only ROM/image content is reconstructed by re-attaching the embedded <c>.crt</c> on
/// restore, so only the mutable state that changes while the program runs is serialized here. The
/// state is an opaque little-endian byte blob owned by the cartridge implementation; the
/// <c>c64-cartridge</c> snapshot module stores and returns it without interpreting it. Multi-byte
/// values should be written/read in a fixed (little-endian) order so snapshots are host-independent.
/// </para>
/// </summary>
public interface ISnapshotableCartridge
{
    byte[] CaptureSnapshotState();
    void RestoreSnapshotState(byte[] state);
}
