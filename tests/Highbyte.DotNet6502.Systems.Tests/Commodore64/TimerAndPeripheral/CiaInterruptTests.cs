using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.TimerAndPeripheral;

public class CiaInterruptTests
{
    private static readonly string TimerBNmiSource = CiaIRQ.GetInterruptSourceName(IRQSource.TimerB);

    [Fact]
    public void Reading_Cia2_Interrupt_Control_Register_Allows_Nmi_To_Retrigger()
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
        }, NullLoggerFactory.Instance);

        c64.Cia2.TimerBLOStore(0, 1);
        c64.Cia2.TimerBHIStore(0, 0);
        c64.Cia2.InterruptControlStore(0, 0x82);
        c64.Cia2.TimerBControlStore(0, 0x11);

        c64.Cia2.ProcessTimers(6);   // force load + start: the latch loads two cycles after the write, is held, then underflows

        Assert.True(c64.CPU.CPUInterrupts.NMIPending);
        Assert.Contains(TimerBNmiSource, c64.CPU.CPUInterrupts.ActiveNMISources);

        c64.CPU.CPUInterrupts.ClearPendingNMI();
        c64.Cia2.InterruptControlLoad(0);

        Assert.DoesNotContain(TimerBNmiSource, c64.CPU.CPUInterrupts.ActiveNMISources);

        c64.Cia2.ProcessTimers(6);   // force load + start: the latch loads two cycles after the write, is held, then underflows

        Assert.True(c64.CPU.CPUInterrupts.NMIPending);
        Assert.Contains(TimerBNmiSource, c64.CPU.CPUInterrupts.ActiveNMISources);
    }

    [Fact]
    public void Cia2_TimerB_Latch_Zero_Counts_Like_A_Latch_Of_One()
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
        }, NullLoggerFactory.Instance);

        c64.Cia2.TimerBLOStore(0, 0);
        c64.Cia2.TimerBHIStore(0, 0);
        c64.Cia2.InterruptControlStore(0, 0x82);
        c64.Cia2.TimerBControlStore(0, 0x11);

        c64.Cia2.ProcessTimers(6);   // force load + start: the latch loads two cycles after the write, is held, then underflows

        Assert.Contains(TimerBNmiSource, c64.CPU.CPUInterrupts.ActiveNMISources);
        Assert.Equal(0x82, c64.Cia2.InterruptControlLoad(0));
        Assert.Equal(0x00, c64.Cia2.InterruptControlLoad(0));

        c64.Cia2.ProcessTimers(2);   // a continuous timer with latch 0 underflows every two cycles

        Assert.Equal(0x02, c64.Cia2.InterruptControlLoad(0) & 0x02);
    }

    [Fact]
    public void Cia2_TimerB_Continuous_Underflow_Remains_Visible_Until_Icr_Read()
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
        }, NullLoggerFactory.Instance);

        c64.Cia2.TimerBLOStore(0, 1);
        c64.Cia2.TimerBHIStore(0, 0);
        c64.Cia2.InterruptControlStore(0, 0x82);
        c64.Cia2.TimerBControlStore(0, 0x11);

        c64.Cia2.ProcessTimers(6);   // force load + start: the latch loads two cycles after the write, is held, then underflows

        Assert.Equal(0x82, c64.Cia2.InterruptControlLoad(0));
        Assert.DoesNotContain(TimerBNmiSource, c64.CPU.CPUInterrupts.ActiveNMISources);
    }

    [Fact]
    public void Cia2_TimerB_Underflow_While_Disabled_Does_Not_Set_Any_Interrupt_Bit()
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
        }, NullLoggerFactory.Instance);

        c64.Cia2.TimerBLOStore(0, 1);
        c64.Cia2.TimerBHIStore(0, 0);
        c64.Cia2.TimerBControlStore(0, 0x11);

        c64.Cia2.ProcessTimers(6);   // force load + start: the latch loads two cycles after the write, is held, then underflows

        Assert.False(c64.CPU.CPUInterrupts.NMIPending);
        Assert.DoesNotContain(TimerBNmiSource, c64.CPU.CPUInterrupts.ActiveNMISources);
        Assert.Equal(0x02, c64.Cia2.InterruptControlLoad(0));
    }

    [Fact]
    public void Cia2_Enabling_A_Source_Whose_Flag_Is_Set_Raises_The_Interrupt()
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
        }, NullLoggerFactory.Instance);

        c64.Cia2.TimerBLOStore(0, 1);
        c64.Cia2.TimerBHIStore(0, 0);
        c64.Cia2.TimerBControlStore(0, 0x19);   // one-shot: a single underflow, while the source is disabled

        c64.Cia2.ProcessTimers(6);
        Assert.DoesNotContain(TimerBNmiSource, c64.CPU.CPUInterrupts.ActiveNMISources);

        c64.Cia2.InterruptControlStore(0, 0x82);   // enable it with its flag still set

        Assert.Contains(TimerBNmiSource, c64.CPU.CPUInterrupts.ActiveNMISources);
        Assert.Equal(0x82, c64.Cia2.InterruptControlLoad(0));
    }

    [Fact]
    public void Cia2_TimerB_Triggered_Interrupt_Keeps_Any_Bit_When_Disabled_Before_Read()
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
        }, NullLoggerFactory.Instance);

        c64.Cia2.TimerBLOStore(0, 1);
        c64.Cia2.TimerBHIStore(0, 0);
        c64.Cia2.InterruptControlStore(0, 0x82);
        c64.Cia2.TimerBControlStore(0, 0x11);

        c64.Cia2.ProcessTimers(6);   // force load + start: the latch loads two cycles after the write, is held, then underflows
        c64.Cia2.InterruptControlStore(0, 0x7f);

        Assert.Equal(0x82, c64.Cia2.InterruptControlLoad(0));
        Assert.DoesNotContain(TimerBNmiSource, c64.CPU.CPUInterrupts.ActiveNMISources);
    }

    private static readonly string TimerANmiSource = CiaIRQ.GetInterruptSourceName(IRQSource.TimerA);

    private static C64 Build() =>
        C64.BuildC64(new C64Config { LoadROMs = false, C64Model = "C64PAL", Vic2Model = "PAL" }, NullLoggerFactory.Instance);

    // Latch 8, force load + start: the latch loads two cycles after the write, shows at cycle 3
    // and counts down from 4, so the timer underflows at cycle 11 (and every 9 cycles after),
    // where its flag shows; the interrupt output follows at cycle 12.
    private const int Underflow = 11;

    private static void StartTimerB(C64 c64, byte interruptControl)
    {
        c64.Cia2.TimerBLOStore(0, 8);
        c64.Cia2.TimerBHIStore(0, 0);
        c64.Cia2.InterruptControlStore(0, interruptControl);
        c64.Cia2.TimerBControlStore(0, 0x11);
    }

    private static void StartTimerA(C64 c64, byte interruptControl)
    {
        c64.Cia2.TimerALOStore(0, 8);
        c64.Cia2.TimerAHIStore(0, 0);
        c64.Cia2.InterruptControlStore(0, interruptControl);
        c64.Cia2.TimerAControlStore(0, 0x11);
    }

    [Fact]
    public void Cia2_TimerB_Flag_Is_Lost_By_An_Icr_Read_In_The_Cycle_Before_Its_Underflow_But_The_Interrupt_Still_Follows()
    {
        var c64 = Build();
        StartTimerB(c64, 0x82);

        c64.Cia2.ProcessTimers(Underflow - 1);
        Assert.Equal(0x00, c64.Cia2.InterruptControlLoad(0));   // the cycle before the underflow: nothing yet, and the flag is lost

        c64.Cia2.ProcessTimers(1);
        Assert.Equal(0x00, c64.Cia2.InterruptControlLoad(0));   // the underflow cycle: the flag never shows

        c64.Cia2.ProcessTimers(2);
        Assert.Contains(TimerBNmiSource, c64.CPU.CPUInterrupts.ActiveNMISources);
        Assert.Equal(0x80, c64.Cia2.InterruptControlLoad(0));   // the interrupt bit alone
    }

    [Fact]
    public void Cia2_TimerA_Flag_Survives_An_Icr_Read_In_The_Cycle_Before_Its_Underflow()
    {
        var c64 = Build();
        StartTimerA(c64, 0x81);

        c64.Cia2.ProcessTimers(Underflow - 1);
        Assert.Equal(0x00, c64.Cia2.InterruptControlLoad(0));

        c64.Cia2.ProcessTimers(1);
        Assert.Equal(0x01, c64.Cia2.InterruptControlLoad(0));   // read away in the underflow cycle: no interrupt follows

        c64.Cia2.ProcessTimers(2);
        Assert.DoesNotContain(TimerANmiSource, c64.CPU.CPUInterrupts.ActiveNMISources);
        Assert.Equal(0x00, c64.Cia2.InterruptControlLoad(0));
    }

    [Fact]
    public void Cia2_Icr_Read_In_The_Cycle_After_A_Read_That_Took_An_Enabled_Flag_Shows_The_Interrupt_Bit_Without_Driving_The_Output()
    {
        var c64 = Build();
        StartTimerA(c64, 0x81);

        c64.Cia2.ProcessTimers(Underflow);
        Assert.Equal(0x01, c64.Cia2.InterruptControlLoad(0));   // the underflow cycle

        c64.Cia2.ProcessTimers(1);
        Assert.Equal(0x80, c64.Cia2.InterruptControlLoad(0));   // the cycle after
        Assert.DoesNotContain(TimerANmiSource, c64.CPU.CPUInterrupts.ActiveNMISources);
        Assert.False(c64.CPU.CPUInterrupts.NMIPending);

        c64.Cia2.ProcessTimers(1);
        Assert.Equal(0x00, c64.Cia2.InterruptControlLoad(0));
    }

    [Fact]
    public void Cia2_Icr_Read_In_The_Cycle_After_A_Read_That_Took_A_Disabled_Flag_Shows_Nothing()
    {
        var c64 = Build();
        StartTimerA(c64, 0x01);   // timer A disabled

        c64.Cia2.ProcessTimers(Underflow);
        Assert.Equal(0x01, c64.Cia2.InterruptControlLoad(0));

        c64.Cia2.ProcessTimers(1);
        Assert.Equal(0x00, c64.Cia2.InterruptControlLoad(0));
    }

    [Fact]
    public void Cia2_Starting_A_Timer_Leaves_Its_Flag_Set()
    {
        var c64 = Build();
        c64.Cia2.TimerALOStore(0, 1);
        c64.Cia2.TimerAHIStore(0, 0);
        c64.Cia2.TimerAControlStore(0, 0x19);   // one-shot: a single underflow, then stopped

        c64.Cia2.ProcessTimers(6);
        c64.Cia2.TimerAControlStore(0, 0x01);   // start again

        Assert.Equal(0x01, c64.Cia2.InterruptControlLoad(0));
    }
}
