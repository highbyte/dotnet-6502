namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Shared snapshot module for the 6502 CPU core (<see cref="CPU"/>). Reused by every system
/// that pairs the CPU with system-specific memory/chip modules (Generic, C64, VIC-20, ...).
/// It captures CPU registers/flags and interrupt state only — never memory, which real
/// machines pair with system-specific RAM/ROM/I-O wiring.
///
/// <para>
/// Capture (v3): CPU model id, then the v1 payload: PC, SP, A, X, Y, processor-status byte,
/// compatibility profile, halted flag, <see cref="ExecState"/> totals, and
/// <see cref="CPUInterrupts"/> (pending-NMI flag plus active IRQ/NMI sources) — followed by
/// the model-state payload (v3: e.g. the 6510's port registers, serialized by
/// <see cref="CpuModelState.SerializeState"/>; null for stateless models). Restore applies
/// registers, flags, interrupt sources and the <see cref="ExecState"/> totals through the
/// CPU's public API. A CPU MODEL mismatch is a hard error (the saved execution state of one
/// chip is not meaningful on another) and is checked first, before any state is applied;
/// a compatibility-PROFILE mismatch stays a warning. The halted flag is captured for
/// diagnostics only. v1 payloads (no model id) restore as nmos6502 — the only model that
/// existed when v1 was written; v2 payloads have no model state here (a 6510 port captured
/// by v2-era code lives in the machine's own module).
/// </para>
///
/// <para>
/// The cumulative cycle count is restored rather than restarted at zero, because peripherals
/// across machines time themselves against it with <em>absolute</em> stamps — the Apple II disk
/// motor's spin-down, its paddle one-shots and its speaker toggles, and the C64's SwiftLink
/// receive pacing. Restarting the counter would put every such stamp in the future, so timers
/// that had long expired would read as though they were still running. Execution limits are
/// unaffected: they are evaluated against the per-invocation <see cref="ExecState"/> that
/// <c>CPU.Execute</c> accumulates, not against these totals.
/// </para>
/// </summary>
public sealed class Cpu6502SnapshotModule : ISnapshotModule
{
    public const string ModuleName = "cpu-6502";

    public string Name => ModuleName;
    public int Version => 3;
    public bool Required => true;

    /// <summary>
    /// Whether the execution state this module carries (registers, flags, interrupt
    /// state) transfers exactly between the captured and target CPU models. Beyond
    /// identity, the NMOS-core family qualifies: the 6510 is an NMOS 6502 core with an
    /// added I/O port — snapshots captured before a machine (the C64) switched its
    /// model id from nmos6502 to mos6510 restore onto the new model unchanged. Model
    /// state (the 6510 port) transfers via the v3 payload when both sides have it;
    /// one-sided cases restore with a warning (see RestoreModelState).
    /// </summary>
    private static bool AreModelsStateCompatible(string capturedModelId, string targetModelId)
        => capturedModelId == targetModelId
            || (IsNmosCore(capturedModelId) && IsNmosCore(targetModelId));

    private static bool IsNmosCore(string modelId)
        => modelId is CpuModelIds.Nmos6502 or CpuModelIds.Mos6510;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var cpu = context.System.CPU;

        // v2: model id first, so restore can reject a model mismatch before applying anything.
        writer.WriteString(cpu.CpuModelId);

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

        // Wire format is unchanged: names plus acknowledge mode, so the line bit layout
        // (which depends on registration order) never leaks into the snapshot.
        var activeIrqSources = interrupts.ActiveIRQSources.ToList();
        writer.WriteInt32(activeIrqSources.Count);
        foreach (var irq in activeIrqSources)
        {
            writer.WriteString(irq.Key);
            writer.WriteBool(irq.Value); // autoAcknowledge
        }

        var activeNmiSources = interrupts.ActiveNMISources.ToList();
        writer.WriteInt32(activeNmiSources.Count);
        foreach (var nmi in activeNmiSources)
            writer.WriteString(nmi);

        // v3: model-state payload, serialized by the model's own state object (e.g. the
        // 6510's port registers). Null for models without state. CHIP state only — board
        // wiring is re-established by the target machine.
        writer.WriteBytes(cpu.ModelState?.SerializeState());
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var cpu = context.System.CPU;

        // The stored module version decides the payload layout (v1 has no model id).
        var storedVersion = context.Manifest.Modules.FirstOrDefault(m => m.Name == ModuleName)?.Version ?? 1;
        var capturedModelId = storedVersion >= 2
            ? reader.ReadString() ?? CpuModelIds.Nmos6502
            : CpuModelIds.Nmos6502; // v1 predates CPU models; only the NMOS 6502 existed

        // CPU model mismatch is a HARD error: registers/flags/interrupt state saved on one
        // chip are not meaningful execution state on another. Checked before any state is
        // applied, so the target system is left untouched.
        if (!AreModelsStateCompatible(capturedModelId, cpu.CpuModelId))
            throw new SnapshotIncompatibleException(SnapshotCompatibility.Incompatible(
                $"cpu-6502: snapshot was captured on CPU model '{capturedModelId}' but the target system's CPU is '{cpu.CpuModelId}'. Configure the system with the matching CPU model and try again."));

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

        // ExecState totals are restored so the cumulative cycle count continues from the saved
        // machine rather than restarting at zero. Peripherals that time themselves against it hold
        // absolute cycle stamps, which are only meaningful against a continuous counter — see
        // ExecState.RestoreTotals.
        var cyclesConsumed = reader.ReadUInt64();
        var instructionsExecutionCount = reader.ReadUInt64();
        var unknownOpCodeCount = reader.ReadUInt64();
        cpu.ExecState.RestoreTotals(cyclesConsumed, instructionsExecutionCount, unknownOpCodeCount);

        RestoreInterrupts(reader, cpu);

        if (capturedHalted && !cpu.IsHalted)
            context.AddWarning("cpu-6502: snapshot CPU was halted; halted state cannot be restored and was ignored.");

        RestoreModelState(reader, cpu, storedVersion, capturedModelId, context);
    }

    /// <summary>
    /// v3+: applies the model-state payload to the target CPU's model state. The
    /// NMOS-core equivalence rule means a payload can be present without the target
    /// having state (mos6510 → nmos6502) or vice versa (nmos6502 → mos6510) — both
    /// restore with a warning instead of failing, since the core execution state
    /// transferred fully. Pre-v3 payloads carry no model state here (a 6510 port
    /// captured by older code lives in the machine's own module, e.g. c64-core v1).
    /// </summary>
    private static void RestoreModelState(SnapshotModuleReader reader, CPU cpu, int storedVersion, string capturedModelId, SnapshotRestoreContext context)
    {
        if (storedVersion < 3)
            return;

        var modelStateBytes = reader.ReadBytes();
        if (modelStateBytes is null)
        {
            if (cpu.ModelState is not null)
                context.AddWarning(
                    $"cpu-6502: snapshot (model '{capturedModelId}') has no model-state payload but the target CPU '{cpu.CpuModelId}' has model state; target keeps its current values.");
            return;
        }

        if (cpu.ModelState is null)
        {
            context.AddWarning(
                $"cpu-6502: snapshot (model '{capturedModelId}') carries a model-state payload but the target CPU '{cpu.CpuModelId}' has none; payload ignored.");
            return;
        }

        cpu.ModelState.RestoreState(modelStateBytes);
    }

    private static void RestoreInterrupts(SnapshotModuleReader reader, CPU cpu)
    {
        var interrupts = cpu.CPUInterrupts;
        var capturedNmiPending = reader.ReadBool();

        // Reset any interrupt state on the freshly built CPU before re-applying the snapshot's.
        interrupts.ClearAll();

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
