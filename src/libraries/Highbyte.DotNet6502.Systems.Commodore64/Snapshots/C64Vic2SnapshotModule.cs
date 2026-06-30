using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Commodore64.Snapshots;

/// <summary>
/// Snapshot module for VIC-II display state that is not held in the C64 IO register storage.
///
/// <para>
/// The VIC-II <em>register bytes</em> ($D000-$D02E) and color RAM ($D800-$DBFF) live in the C64
/// <see cref="C64.IO"/> array and are restored by the <c>c64-core</c> module. However, the VIC-II
/// keeps <em>cached/derived</em> state that is only recomputed inside the register write handlers:
/// the screen-memory pointer (<see cref="Vic2.VideoMatrixBaseAddress"/>), the character/bitmap base
/// address, and the VIC bank (<see cref="Vic2.CurrentVIC2Bank"/>, driven by CIA2 $DD00). Because
/// <c>c64-core</c> copies the IO bytes directly into the backing array (bypassing the handlers),
/// this module re-derives that cached state from the already-restored registers on restore. Without
/// it, a restored machine renders from the wrong memory addresses (full-screen garbage).
/// </para>
///
/// <para>
/// The live raster position is captured for diagnostics/forward-compatibility but not forced back:
/// it is re-derived as the restored machine runs, and the per-frame renderer state rebuilds itself
/// once execution resumes.
/// </para>
/// </summary>
public sealed class C64Vic2SnapshotModule : ISnapshotModule
{
    public const string ModuleName = "c64-vic2";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var vic2 = ((C64)context.System).Vic2;

        // Live raster position (diagnostics / forward-compatibility only).
        writer.WriteUInt16(vic2.CurrentRasterLine);
        writer.WriteUInt64(vic2.CyclesConsumedCurrentVblank);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var c64 = (C64)context.System;
        var vic2 = c64.Vic2;

        _ = reader.ReadUInt16(); // CurrentRasterLine (re-derived during execution)
        _ = reader.ReadUInt64(); // CyclesConsumedCurrentVblank (re-derived during execution)

        // Re-derive the cached VIC-II display state from the registers restored by c64-core.
        // Order matters: the VIC bank (CIA2 $DD00) selects the 16 KB window and refreshes the
        // charset address; then $D018 sets the screen/charset/bitmap base addresses within it.
        vic2.SetVIC2Bank(c64.ReadIOStorage(CiaAddr.CIA2_DATAA));
        vic2.MemorySetupStore(Vic2Addr.MEMORY_SETUP, c64.ReadIOStorage(Vic2Addr.MEMORY_SETUP));

        // Force the sprite manager to rebuild cached sprite state from the restored registers/RAM.
        vic2.SpriteManager.SetAllChanged(Vic2Sprite.Vic2SpriteChangeType.All);
    }
}
