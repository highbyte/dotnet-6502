using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.TimerAndPeripheral;

public class CiaInterruptTests
{
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

        c64.Cia2.ProcessTimers(2);

        Assert.True(c64.CPU.CPUInterrupts.NMIPending);
        Assert.Contains("TimerB", c64.CPU.CPUInterrupts.ActiveNMISources);

        c64.CPU.CPUInterrupts.ClearPendingNMI();
        c64.Cia2.InterruptControlLoad(0);

        Assert.DoesNotContain("TimerB", c64.CPU.CPUInterrupts.ActiveNMISources);

        c64.Cia2.ProcessTimers(2);

        Assert.True(c64.CPU.CPUInterrupts.NMIPending);
        Assert.Contains("TimerB", c64.CPU.CPUInterrupts.ActiveNMISources);
    }

    [Fact]
    public void Cia2_TimerB_Latch_Zero_Does_Not_Immediately_Underflow()
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

        c64.Cia2.ProcessTimers(2);

        Assert.False(c64.CPU.CPUInterrupts.NMIPending);
        Assert.DoesNotContain("TimerB", c64.CPU.CPUInterrupts.ActiveNMISources);
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

        c64.Cia2.ProcessTimers(2);

        Assert.Equal(0x82, c64.Cia2.InterruptControlLoad(0));
        Assert.DoesNotContain("TimerB", c64.CPU.CPUInterrupts.ActiveNMISources);
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

        c64.Cia2.ProcessTimers(2);

        Assert.False(c64.CPU.CPUInterrupts.NMIPending);
        Assert.DoesNotContain("TimerB", c64.CPU.CPUInterrupts.ActiveNMISources);
        Assert.Equal(0x02, c64.Cia2.InterruptControlLoad(0));
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

        c64.Cia2.ProcessTimers(2);
        c64.Cia2.InterruptControlStore(0, 0x7f);

        Assert.Equal(0x82, c64.Cia2.InterruptControlLoad(0));
        Assert.DoesNotContain("TimerB", c64.CPU.CPUInterrupts.ActiveNMISources);
    }
}
