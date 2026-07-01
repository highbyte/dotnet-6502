namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Shared snapshot module for the 6502 CPU core (<see cref="CPU"/>). Reused by every system
/// that pairs the CPU with system-specific memory/chip modules (Generic, C64, VIC-20, ...).
/// It captures CPU registers/flags and interrupt state only — never memory, which real
/// machines pair with system-specific RAM/ROM/I-O wiring.
///
/// <para>
/// Capture (v1): PC, SP, A, X, Y, processor-status byte, compatibility profile, halted flag,
/// <see cref="ExecState"/> totals, and <see cref="CPUInterrupts"/> (pending-NMI flag plus
/// active IRQ/NMI sources). Restore applies registers, flags and interrupt sources through the
/// CPU's public API. The compatibility profile is validated against the rebuilt CPU; the
/// halted flag and <see cref="ExecState"/> totals are captured for forward-compatibility and
/// diagnostics but are re-derived as the system runs rather than forced back onto the CPU.
/// </para>
/// </summary>
public sealed class Cpu6502SnapshotModule : ISnapshotModule
{
    public const string ModuleName = "cpu-6502";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var cpu = context.System.CPU;

        writer.WriteUInt16(cpu.PC);
        writer.WriteByte(cpu.SP);
        writer.WriteByte(cpu.A);
        writer.WriteByte(cpu.X);
        writer.WriteByte(cpu.Y);
        writer.WriteByte(cpu.ProcessorStatus.Value);

        writer.WriteInt32((int)cpu.CompatibilityProfile);
        writer.WriteBool(cpu.IsHalted);

        // ExecState totals (diagnostics / forward-compatibility).
        writer.WriteUInt64(cpu.ExecState.CyclesConsumed);
        writer.WriteUInt64(cpu.ExecState.InstructionsExecutionCount);
        writer.WriteUInt64(cpu.ExecState.UnknownOpCodeCount);

        // Interrupt state.
        var interrupts = cpu.CPUInterrupts;
        writer.WriteBool(interrupts.NMIPending);

        writer.WriteInt32(interrupts.ActiveIRQSources.Count);
        foreach (var irq in interrupts.ActiveIRQSources)
        {
            writer.WriteString(irq.Key);
            writer.WriteBool(irq.Value); // autoAcknowledge
        }

        writer.WriteInt32(interrupts.ActiveNMISources.Count);
        foreach (var nmi in interrupts.ActiveNMISources)
            writer.WriteString(nmi);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var cpu = context.System.CPU;

        cpu.PC = reader.ReadUInt16();
        cpu.SP = reader.ReadByte();
        cpu.A = reader.ReadByte();
        cpu.X = reader.ReadByte();
        cpu.Y = reader.ReadByte();
        cpu.ProcessorStatus = new ProcessorStatus(reader.ReadByte());

        var capturedProfile = (CpuCompatibilityProfile)reader.ReadInt32();
        if (capturedProfile != cpu.CompatibilityProfile)
            context.AddWarning(
                $"cpu-6502: snapshot CPU compatibility profile '{capturedProfile}' differs from target '{cpu.CompatibilityProfile}'; using target profile.");

        var capturedHalted = reader.ReadBool();

        // ExecState totals are read to keep the stream aligned and for potential diagnostics;
        // they are re-derived as the restored system runs and are not forced back onto the CPU.
        _ = reader.ReadUInt64(); // CyclesConsumed
        _ = reader.ReadUInt64(); // InstructionsExecutionCount
        _ = reader.ReadUInt64(); // UnknownOpCodeCount

        RestoreInterrupts(reader, cpu);

        if (capturedHalted && !cpu.IsHalted)
            context.AddWarning("cpu-6502: snapshot CPU was halted; halted state cannot be restored and was ignored.");
    }

    private static void RestoreInterrupts(SnapshotModuleReader reader, CPU cpu)
    {
        var interrupts = cpu.CPUInterrupts;
        var capturedNmiPending = reader.ReadBool();

        // Reset any interrupt state on the freshly built CPU before re-applying the snapshot's.
        interrupts.ActiveIRQSources.Clear();
        interrupts.ActiveNMISources.Clear();
        interrupts.ClearPendingNMI();

        int irqCount = reader.ReadInt32();
        for (int i = 0; i < irqCount; i++)
        {
            var source = reader.ReadString() ?? "";
            var autoAcknowledge = reader.ReadBool();
            interrupts.SetIRQSourceActive(source, autoAcknowledge);
        }

        int nmiCount = reader.ReadInt32();
        for (int i = 0; i < nmiCount; i++)
        {
            var source = reader.ReadString() ?? "";
            interrupts.SetNMISourceActive(source); // sets NMIPending = true as a side effect
        }

        // SetNMISourceActive latches NMIPending whenever a new source is added. Reconcile with
        // the captured pending flag (a real edge may have been serviced after the source was set).
        if (!capturedNmiPending)
            interrupts.ClearPendingNMI();
    }
}
