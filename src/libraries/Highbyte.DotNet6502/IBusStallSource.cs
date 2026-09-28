namespace Highbyte.DotNet6502;

/// <summary>
/// Lets a system stall the CPU on a read, the way a bus master that holds BA/RDY low does (the
/// C64's VIC-II during bad lines and sprite DMA). The 6510 keeps executing writes while RDY is
/// low and freezes on its next read until the line is released, so only reads consult this.
/// </summary>
public interface IBusStallSource
{
    /// <summary>
    /// Called before a read that happens at <paramref name="busCycle"/> (the 1-based cycle number
    /// the read would occupy, see <see cref="CPU.BusCycles"/>). Returns how many cycles the CPU
    /// waits before the read takes place: 0 if the bus is free. <paramref name="nextCheckBusCycle"/>
    /// is the earliest bus cycle at which the source wants to be consulted again; the CPU skips the
    /// call for reads before it. <see cref="ulong.MaxValue"/> means never, until
    /// <see cref="CPU.RequestBusStallCheck"/> is called.
    /// </summary>
    ulong StallCyclesForRead(ulong busCycle, out ulong nextCheckBusCycle);
}
