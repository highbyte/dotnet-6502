using Highbyte.DotNet6502.Systems.Snapshots;
using Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Systems.Vic20.Snapshots;

/// <summary>
/// Snapshot module for the VIC-20 VIA chips (VIA1 + VIA2) — the live state held outside the
/// memory map: data-direction registers, the auxiliary control register, Timer 1 (latch/counter/
/// running/free-running) and the IRQ enable/condition flags, plus VIA2's keyboard column-strobe
/// latch (Port B). VIA1's Timer 1 drives the CA1 raster-pulse IRQ the KERNAL uses for keyboard
/// scan and cursor blink, so this state must be restored for input to resume.
/// </summary>
public sealed class Vic20ViaSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "vic20-via";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var vic20 = (Vic20)context.System;
        CaptureVia(writer, vic20.Via1);
        CaptureVia(writer, vic20.Via2);
        writer.WriteByte(vic20.Via2.SnapshotPortBValue);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var vic20 = (Vic20)context.System;
        RestoreVia(reader, vic20.Via1);
        RestoreVia(reader, vic20.Via2);
        vic20.Via2.SnapshotPortBValue = reader.ReadByte();
    }

    private static void CaptureVia(SnapshotModuleWriter writer, ViaBase via)
    {
        writer.WriteByte(via.SnapshotDdra);
        writer.WriteByte(via.SnapshotDdrb);
        writer.WriteByte(via.SnapshotAcr);

        var (latch, counter, running, freeRunning) = via.SnapshotTimer1.GetSnapshotState();
        writer.WriteUInt16(latch);
        writer.WriteUInt16(counter);
        writer.WriteBool(running);
        writer.WriteBool(freeRunning);

        var (t1Enabled, t1Condition, ca1Enabled, ca1Condition) = via.SnapshotIrq.GetSnapshotState();
        writer.WriteBool(t1Enabled);
        writer.WriteBool(t1Condition);
        writer.WriteBool(ca1Enabled);
        writer.WriteBool(ca1Condition);
    }

    private static void RestoreVia(SnapshotModuleReader reader, ViaBase via)
    {
        via.SnapshotDdra = reader.ReadByte();
        via.SnapshotDdrb = reader.ReadByte();
        via.SnapshotAcr = reader.ReadByte();

        var latch = reader.ReadUInt16();
        var counter = reader.ReadUInt16();
        var running = reader.ReadBool();
        var freeRunning = reader.ReadBool();
        via.SnapshotTimer1.RestoreSnapshotState(latch, counter, running, freeRunning);

        var t1Enabled = reader.ReadBool();
        var t1Condition = reader.ReadBool();
        var ca1Enabled = reader.ReadBool();
        var ca1Condition = reader.ReadBool();
        via.SnapshotIrq.RestoreSnapshotState(t1Enabled, t1Condition, ca1Enabled, ca1Condition);
    }
}
