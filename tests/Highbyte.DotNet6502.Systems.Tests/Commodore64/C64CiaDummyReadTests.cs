using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

/// <summary>
/// The C64 motivating case for NMOS indexed dummy reads: the CIA interrupt control
/// register ($DC0D) clears its pending flags on ANY read — including the dummy read an
/// indexed store performs before writing. `STA $DC0D,X` with X=0 therefore silently
/// acknowledges pending CIA interrupts on real hardware, a classic C64 gotcha that only
/// exists when the emulator performs real bus access sequences.
/// </summary>
public class C64CiaDummyReadTests
{
    [Fact]
    public void Indexed_Store_To_ICR_Clears_Pending_Flags_Via_Its_Dummy_Read()
    {
        var c64 = BuildC64();
        RaiseTimerAInterruptFlag(c64);

        // STA $DC0D,X with X=0: the dummy read at the (un-carried == effective) address
        // reads the ICR, clearing the pending flags, before the write stores the mask.
        c64.Mem[0x2000] = 0x9D; // STA abs,X
        c64.Mem[0x2001] = 0x0D;
        c64.Mem[0x2002] = 0xDC;
        c64.CPU.PC = 0x2000;
        c64.CPU.X = 0x00;
        c64.CPU.A = 0x00; // ICR mask write with bit 7 clear: clears no-op mask bits only
        c64.CPU.ExecuteOneInstructionMinimal(c64.Mem);

        // The flags are gone: a subsequent ICR read reports nothing pending.
        Assert.Equal(0, c64.Mem[CiaAddr.CIA1_CIAICR] & 0b0000_0001);
    }

    [Fact]
    public void Control_Case_Without_The_Store_The_Flag_Is_Pending_Until_Read()
    {
        var c64 = BuildC64();
        RaiseTimerAInterruptFlag(c64);

        // First ICR read reports the pending timer A flag (and clears it)...
        Assert.Equal(1, c64.Mem[CiaAddr.CIA1_CIAICR] & 0b0000_0001);
        // ...second read shows it was clear-on-read.
        Assert.Equal(0, c64.Mem[CiaAddr.CIA1_CIAICR] & 0b0000_0001);
    }

    /// <summary>
    /// Raises the CIA1 timer A interrupt flag through the public register interface:
    /// program a 1-tick timer, start it, and advance time past the underflow.
    /// (The interrupt MASK stays disabled, so only the flag is set — no IRQ fires.)
    /// </summary>
    private static void RaiseTimerAInterruptFlag(C64 c64)
    {
        c64.Mem[CiaAddr.CIA1_TIMALO] = 0x01;
        c64.Mem[CiaAddr.CIA1_TIMAHI] = 0x00;
        c64.Mem[CiaAddr.CIA1_CIACRA] = 0b0000_0001; // start timer A
        c64.Cia1.ProcessTimers(cyclesExecuted: 3);
    }

    private static C64 BuildC64()
        => C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL"
        }, NullLoggerFactory.Instance);
}
