namespace Highbyte.DotNet6502;

/// <summary>CPU state saved alongside the registers to resume execution between instructions.</summary>
/// <remarks>
/// A snapshot must preserve what the CPU observed at its last interrupt poll. For example,
/// <c>CLI</c> clears the interrupt-disable flag after the poll. Using only the restored
/// status register could make the CPU take a pending IRQ one instruction too early.
/// </remarks>
/// <param name="BusCycles">CPU bus clock at capture. All cycle values below use this clock.</param>
/// <param name="InterruptPollBusCycle">Cycle used to decide which interrupt signals the CPU saw at this boundary.</param>
/// <param name="InterruptDisableAtPoll">Interrupt-disable flag sampled before an instruction changed it.
/// A null value tells the CPU to use the flag in the status register.</param>
/// <param name="HijackableEntryPending">True after IRQ or BRK entry when an NMI that arrived
/// by the vector-selection cycle can still redirect execution using the stack frame already pushed.</param>
/// <param name="Halted">Whether the CPU was halted at capture.</param>
/// <param name="IrqAssertedAt">Bus cycle when the first active IRQ source asserted the shared line.</param>
/// <param name="IrqReleasedAt">Bus cycle when the last active IRQ source released the shared line.</param>
/// <param name="NmiPendingAt">Bus cycle when the pending NMI edge was detected, if one is pending.</param>
public readonly record struct CpuExecutionBoundaryState(
    ulong BusCycles, ulong InterruptPollBusCycle, bool? InterruptDisableAtPoll,
    bool HijackableEntryPending, bool Halted, ulong IrqAssertedAt, ulong IrqReleasedAt, ulong NmiPendingAt);
