namespace Highbyte.DotNet6502;

public partial class CPU
{
    public CpuExecutionBoundaryState CaptureExecutionBoundaryState() => new(
        BusCycles, _interruptPollBusCycle, _interruptDisableAtPoll, _hijackableEntryPending,
        IsHalted, CPUInterrupts.IRQAssertedAtBusCycle, CPUInterrupts.IRQReleasedAtBusCycle,
        CPUInterrupts.NMIPendingAtBusCycle);

    /// <summary>Restore only between instructions, before resuming peripheral clocks.</summary>
    public void RestoreExecutionBoundaryState(CpuExecutionBoundaryState state)
    {
        if (state.InterruptPollBusCycle > state.BusCycles)
            throw new ArgumentException("Interrupt poll cannot be later than the CPU bus clock.", nameof(state));
        BusCycles = state.BusCycles;
        _interruptPollBusCycle = state.InterruptPollBusCycle;
        _interruptDisableAtPoll = state.InterruptDisableAtPoll;
        _hijackableEntryPending = state.HijackableEntryPending;
        IsHalted = state.Halted;
        CPUInterrupts.RestoreTiming(state.IrqAssertedAt, state.IrqReleasedAt, state.NmiPendingAt);
        RequestBusStallCheck();
    }
}
