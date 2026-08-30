using Highbyte.DotNet6502.Systems.Oric;
using Highbyte.DotNet6502.Systems.Oric.Config;
using Highbyte.DotNet6502.Systems.Oric.Hardware;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricVSyncHackTests
{
    [Fact]
    public void VSyncHackIsDisabledByDefault()
    {
        Assert.False(new OricConfig().VSyncHackEnabled);
        Assert.False(new OricSystemConfig().VSyncHackEnabled);
    }

    [Fact]
    public void FrameBoundaryGeneratesFallingCb1EdgeAfterVSyncDelay()
    {
        var oric = CreateOric(vSyncHackEnabled: true);
        oric.Mem[0x030c] = 0x00; // CB1 interrupt on falling edge.

        oric.ExecuteOneFrame();

        Assert.Equal(0, oric.Via.InterruptFlags & Via6522.InterruptCb1);
        ExecuteAtLeastCycles(oric, OricMachine.VSyncHackDelayCycles);
        Assert.Equal(Via6522.InterruptCb1, oric.Via.InterruptFlags & Via6522.InterruptCb1);
    }

    [Fact]
    public void VSyncPulseReturnsHighAfterLowInterval()
    {
        var oric = CreateOric(vSyncHackEnabled: true);
        oric.Mem[0x030c] = 0x00; // Start by detecting the falling edge.
        oric.ExecuteOneFrame();
        ExecuteAtLeastCycles(oric, OricMachine.VSyncHackDelayCycles);
        _ = oric.Mem[0x0300]; // Clear CB1 interrupt by reading port B.
        oric.Mem[0x030c] = 0x10; // Detect the rising edge ending the pulse.

        ExecuteAtLeastCycles(oric, OricMachine.VSyncHackLowCycles);

        Assert.Equal(Via6522.InterruptCb1, oric.Via.InterruptFlags & Via6522.InterruptCb1);
    }

    [Fact]
    public void DisabledVSyncHackDoesNotDriveCb1()
    {
        var oric = CreateOric(vSyncHackEnabled: false);
        oric.Mem[0x030c] = 0x00;

        oric.ExecuteOneFrame();
        ExecuteAtLeastCycles(oric, OricMachine.VSyncHackDelayCycles + OricMachine.VSyncHackLowCycles);

        Assert.Equal(0, oric.Via.InterruptFlags & Via6522.InterruptCb1);
    }

    private static OricMachine CreateOric(bool vSyncHackEnabled) =>
        new(new OricConfig { VSyncHackEnabled = vSyncHackEnabled }, new NullLoggerFactory());

    private static void ExecuteAtLeastCycles(OricMachine oric, int minimumCycles)
    {
        ulong cycles = 0;
        while (cycles < (ulong)minimumCycles)
        {
            oric.ExecuteOneInstruction(out var instruction);
            cycles += instruction.CyclesConsumed;
        }
    }
}
