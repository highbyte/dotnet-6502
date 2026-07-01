using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Commodore64.Snapshots;

/// <summary>
/// Snapshot module for the live CIA1 + CIA2 state that is not held in the C64 IO register storage:
/// each chip's Timer A/B live counter/latch/control/running state and the interrupt
/// enable/condition flags.
///
/// <para>
/// The CIA register bytes (timer latches, etc.) live in <see cref="C64.IO"/> and are restored by
/// <c>c64-core</c>, but the running counters, the timer control/running flags and the IRQ mask are
/// internal to the <see cref="CiaTimer"/>/<see cref="CiaIRQ"/> objects. Without restoring them the
/// 60 Hz keyboard-scan IRQ (CIA1 Timer A) never resumes, so a restored machine accepts no input.
/// </para>
/// </summary>
public sealed class C64CiaSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "c64-cia";

    // Interrupt sources whose enable/condition flags are persisted (the meaningful CIA sources;
    // IRQSource.Any is a derived latch, recomputed by the CIA, so it is not persisted).
    private static readonly IRQSource[] s_irqSources =
    {
        IRQSource.TimerA,
        IRQSource.TimerB,
        IRQSource.TimeOfDayAlarm,
        IRQSource.SerialShiftRegister,
        IRQSource.FlagLine,
    };

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var c64 = (C64)context.System;
        CaptureCia(writer, c64.Cia1);
        CaptureCia(writer, c64.Cia2);

        // CIA1 keyboard/joystick port + DDR registers are cached outside IO storage.
        var (portA, portB, ddra, ddrb) = c64.Cia1.GetSnapshotPortState();
        writer.WriteByte(portA);
        writer.WriteByte(portB);
        writer.WriteByte(ddra);
        writer.WriteByte(ddrb);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var c64 = (C64)context.System;
        RestoreCia(reader, c64.Cia1);
        RestoreCia(reader, c64.Cia2);

        var portA = reader.ReadByte();
        var portB = reader.ReadByte();
        var ddra = reader.ReadByte();
        var ddrb = reader.ReadByte();
        c64.Cia1.RestoreSnapshotPortState(portA, portB, ddra, ddrb);
    }

    private static void CaptureCia(SnapshotModuleWriter writer, CiaBase cia)
    {
        CaptureTimer(writer, cia.SnapshotTimerA);
        CaptureTimer(writer, cia.SnapshotTimerB);

        var irq = cia.SnapshotIrq;
        foreach (var source in s_irqSources)
        {
            writer.WriteBool(irq.IsEnabled(source));
            writer.WriteBool(irq.IsConditionSet(source));
        }
    }

    private static void RestoreCia(SnapshotModuleReader reader, CiaBase cia)
    {
        RestoreTimer(reader, cia.SnapshotTimerA);
        RestoreTimer(reader, cia.SnapshotTimerB);

        var irq = cia.SnapshotIrq;
        foreach (var source in s_irqSources)
        {
            var enabled = reader.ReadBool();
            var condition = reader.ReadBool();

            if (enabled)
                irq.Enable(source);
            else
                irq.Disable(source);

            if (condition)
                irq.ConditionSet(source);
            else
                irq.ConditionClear(source);
        }
    }

    private static void CaptureTimer(SnapshotModuleWriter writer, CiaTimer timer)
    {
        var (latch, control, current, running) = timer.GetSnapshotState();
        writer.WriteUInt16(latch);
        writer.WriteByte(control);
        writer.WriteUInt16(current);
        writer.WriteBool(running);
    }

    private static void RestoreTimer(SnapshotModuleReader reader, CiaTimer timer)
    {
        var latch = reader.ReadUInt16();
        var control = reader.ReadByte();
        var current = reader.ReadUInt16();
        var running = reader.ReadBool();
        timer.RestoreSnapshotState(latch, control, current, running);
    }
}
