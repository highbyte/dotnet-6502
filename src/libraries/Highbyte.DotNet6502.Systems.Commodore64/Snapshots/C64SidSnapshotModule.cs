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
/// Internal oscillator phase and envelope position are not preserved (v1), so a restored sustained
/// note restarts from its attack phase — a brief audio transient, not silence.
/// </para>
/// </summary>
public sealed class C64SidSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "c64-sid";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        // SID register values live in the IO array (captured by c64-core); nothing extra to store.
        // A format-version byte keeps the module self-describing and lets the layout grow later.
        writer.WriteInt32(Version);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        _ = reader.ReadInt32(); // format version (reserved)

        var c64 = (C64)context.System;
        c64.Sid.InternalSidState.MarkAllRegistersChangedForSnapshotRestore();
    }
}
