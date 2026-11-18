using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Commodore64.Snapshots;

/// <summary>
/// Snapshot module for the SID audio chip.
///
/// <para>
/// The SID register values are stored in the C64 IO array (the register handlers read/write IO
/// storage), so they are already restored by the <c>c64-core</c> module. However, the C64 audio
/// providers are <em>edge-triggered</em> on SID register changes — a voice only (re)starts when its
/// control register is observed changing. After a restore the change-set is empty, so a voice whose
/// gate was already on before the snapshot would stay silent (sustained sounds), while a freshly
/// written sound would play. This module re-flags the SID registers as changed on restore so the
/// provider re-evaluates the restored state and restarts the active voices.
/// </para>
///
/// <para>
/// v2 preserves the emulated SID core and bus latch, including oscillator/noise/envelope state
/// visible through CPU reads. Restoring v2 uses the saved SID emulation mode for this machine.
/// Legacy v1 snapshots restart voices from their register values.
/// </para>
/// </summary>
public sealed class C64SidSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "c64-sid";

    public string Name => ModuleName;
    public int Version => 2;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var c64 = (C64)context.System;
        writer.WriteInt32(Version);
        c64.Sid.InternalSidState.CaptureBusState(writer);
        writer.WriteUInt64(c64.CPU.BusCycles);
        writer.WriteBytes(c64.SidEmulation.Core.CaptureState());
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var version = reader.ReadInt32();
        var c64 = (C64)context.System;
        if (version >= 2)
        {
            c64.Sid.InternalSidState.RestoreBusState(reader);
            var coreCycles = reader.ReadUInt64();
            c64.SidEmulation.RestoreState(coreCycles, reader.ReadBytes()
                ?? throw new SnapshotException("c64-sid: missing SID core state."));
        }
        else
            c64.SidEmulation.RestoreLegacyRegisters();
        // Command consumers (including ones attached after restore) use these notifications
        // to restart sustained voices. The sample core already has their precise phases.
        if (ReferenceEquals(c64.AudioProvider, c64.SidEmulation))
            c64.Sid.InternalSidState.ClearAudioChanged();
        else
            c64.Sid.InternalSidState.MarkAllRegistersChangedForSnapshotRestore();
    }
}
