using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Recording;

/// <summary>CPU state used to compare two executions at a replay checkpoint.
/// Interrupt sources are ordered by name so registration order cannot affect the digest.</summary>
public static class Cpu6502Checkpoint
{
    public static void Write(BinaryWriter writer, CPU cpu)
    {
        var fields = new SnapshotModuleWriter(writer);
        Cpu6502SnapshotModule.WriteRegisters(fields, cpu);
        writer.Write(cpu.IsHalted);
        writer.Write(cpu.ExecState.CyclesConsumed); // Absolute chip timestamps are relative to this clock.
        Cpu6502SnapshotModule.WriteBoundaryTiming(fields, cpu);
        writer.Write(cpu.CPUInterrupts.NMIPending);
        foreach (var irq in cpu.CPUInterrupts.ActiveIRQSources.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            writer.Write(irq.Key);
            writer.Write(irq.Value);
        }
        writer.Write("");
        foreach (var nmi in cpu.CPUInterrupts.ActiveNMISources.Order(StringComparer.Ordinal))
            writer.Write(nmi);
        writer.Write("");
        writer.Write(cpu.ModelState?.SerializeState() ?? []);
    }
}
