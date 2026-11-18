namespace Highbyte.DotNet6502;

public sealed partial class CPUInterrupts
{
    /// <summary>Restore edge timestamps after restoring the source lines themselves.</summary>
    public void RestoreTiming(ulong irqAssertedAt, ulong irqReleasedAt, ulong nmiPendingAt)
    {
        IRQAssertedAtBusCycle = irqAssertedAt;
        IRQReleasedAtBusCycle = irqReleasedAt;
        NMIPendingAtBusCycle = nmiPendingAt;
    }

    /// <summary>Restore the sampled NMI edge independently of the current source levels.</summary>
    public void RestorePendingNmi(bool pending) => NMIPending = pending;
}
