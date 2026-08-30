using Highbyte.DotNet6502.Systems.Oric;
using Highbyte.DotNet6502.Systems.Oric.Hardware;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricRasterClockTests
{
    [Fact]
    public void AdvancesRasterLinesFromCpuCycles()
    {
        var clock = new OricRasterClock();
        var startedLines = new List<int>();
        clock.RasterLineStarted += startedLines.Add;

        clock.Advance(OricConfig.CyclesPerLine * 44 + 7);

        Assert.Equal(44, clock.RasterLine);
        Assert.Equal(7, clock.CycleInLine);
        Assert.Equal(Enumerable.Range(1, 44), startedLines);
    }

    [Fact]
    public void PreservesInstructionOvershootAcrossFrameBoundary()
    {
        var clock = new OricRasterClock();
        var completedFrames = 0;
        clock.FrameCompleted += () => completedFrames++;

        clock.Advance((int)OricConfig.CpuCyclesPerFrame + 5);

        Assert.Equal(1, completedFrames);
        Assert.Equal(1UL, clock.FrameNumber);
        Assert.Equal(0, clock.RasterLine);
        Assert.Equal(5, clock.CycleInLine);
    }

    [Fact]
    public void ReportsVSyncWaveformAtExactRasterCycles()
    {
        var clock = new OricRasterClock();
        var transitions = new List<(int Cycle, bool High)>();
        clock.VSyncLevelChanged += high => transitions.Add((clock.CycleInFrame, high));

        clock.Advance((int)OricConfig.CpuCyclesPerFrame + OricMachine.VSyncHackDelayCycles - 1);
        Assert.Equal([(0, true)], transitions);

        clock.Advance(1);
        clock.Advance(OricMachine.VSyncHackLowCycles);

        Assert.Equal(
            [(0, true), (OricMachine.VSyncHackDelayCycles, false),
             (OricMachine.VSyncHackDelayCycles + OricMachine.VSyncHackLowCycles, true)],
            transitions);
    }
}
