using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Debugger;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

/// <summary>
/// The C64's debug values (the VIC-II's position: RASTER, CYCLE, FRAMECYCLE, FRAME) and the
/// run-until condition that stops at a raster position.
/// </summary>
public class C64DebugValuesTests
{
    private const ushort Start = 0x1000;

    private static C64 Build(string vic2Model = "PAL")
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = vic2Model == "PAL" ? "C64PAL" : "C64NTSC",
            Vic2Model = vic2Model,
        }, NullLoggerFactory.Instance);
        // NOPs from Start, and a JMP back to Start at the end of the block, so the program loops forever.
        for (var address = Start; address < Start + 0x100; address++)
            c64.Mem[address] = 0xEA;
        c64.Mem.StoreData(Start + 0x100, [0x4C, (byte)(Start & 0xFF), (byte)(Start >> 8)]);
        c64.CPU.PC = Start;
        return c64;
    }

    private static long Value(C64 c64, string name)
    {
        Assert.True(c64.TryGetDebugValue(name, out var value));
        return value;
    }

    [Fact]
    public void Exposes_the_vic2_position_under_its_name()
    {
        var c64 = Build();
        Assert.Equal("VIC-II", c64.DebugValueGroupName);
        Assert.Equal(["RASTER", "CYCLE", "FRAMECYCLE", "FRAME"], c64.DebugValues.Select(v => v.Name));
        Assert.Equal(["raster"], c64.RunUntilTargets.Select(v => v.Name));
        Assert.False(c64.TryGetDebugValue("BORDER", out _));
    }

    [Fact]
    public void Position_values_follow_the_raster_with_the_chips_cycle_numbering()
    {
        var c64 = Build();
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        Assert.Equal(63UL, cyclesPerLine);

        Assert.Equal(0, Value(c64, "RASTER"));
        Assert.Equal(1, Value(c64, "CYCLE"));       // the line's first cycle is cycle 1
        Assert.Equal(0, Value(c64, "FRAMECYCLE"));
        Assert.Equal(0, Value(c64, "FRAME"));

        c64.Vic2.AdvanceRaster(52 * cyclesPerLine + 24);
        Assert.Equal(52, Value(c64, "RASTER"));
        Assert.Equal(25, Value(c64, "cycle"));      // case-insensitive
        Assert.Equal(52 * 63 + 24, Value(c64, "FRAMECYCLE"));

        c64.Vic2.AdvanceRaster(cyclesPerLine - 24 - 1);
        Assert.Equal(52, Value(c64, "RASTER"));
        Assert.Equal(63, Value(c64, "CYCLE"));      // the line's last cycle
        c64.Vic2.AdvanceRaster(1);
        Assert.Equal(53, Value(c64, "RASTER"));
        Assert.Equal(1, Value(c64, "CYCLE"));
    }

    [Fact]
    public void The_system_info_shown_by_hosts_carries_the_same_line_as_the_monitor()
    {
        var c64 = Build();
        c64.Vic2.AdvanceRaster(52 * 63 + 24);
        Assert.Equal("VIC-II: RASTER=52 CYCLE=25 FRAMECYCLE=3300 FRAME=0", c64.SystemInfo[0]);
        Assert.Equal(c64.FormatDebugValuesLine(), c64.SystemInfo[0]);
        Assert.StartsWith("CPU bank: ", c64.SystemInfo[1]);
        Assert.Contains("Model: C64PAL", c64.SystemInfo[1]);
    }

    [Fact]
    public void Frame_counts_the_completed_frames()
    {
        var c64 = Build();
        c64.ExecuteOneFrame();
        Assert.Equal(1, Value(c64, "FRAME"));
        c64.ExecuteOneFrame();
        c64.ExecuteOneFrame();
        Assert.Equal(3, Value(c64, "FRAME"));
        Assert.Equal(0, Value(c64, "RASTER"));      // a frame ends on line 0's first cycles
    }

    [Fact]
    public void Values_are_current_after_a_cpu_only_step()
    {
        // The monitor's single step runs the CPU alone; the VIC-II is brought up to its bus
        // cycle when a value is read, so the position is not stale.
        var c64 = Build();
        c64.CPU.ExecuteOneInstruction(c64.Mem);   // NOP, 2 cycles
        c64.CPU.ExecuteOneInstruction(c64.Mem);
        Assert.Equal(4, Value(c64, "FRAMECYCLE"));
        Assert.Equal(5, Value(c64, "CYCLE"));
    }

    private static ExecEvaluatorTriggerResult RunUntil(C64 c64, string condition, int maxFrames = 3)
    {
        var evaluator = new DebuggerBreakpointEvaluator { DebugValues = c64, RunUntilCondition = condition };
        for (var frame = 0; frame < maxFrames; frame++)
        {
            var result = c64.ExecuteOneFrame(evaluator);
            if (result.Triggered)
                return result;
        }
        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    [Fact]
    public void Run_until_a_raster_position_ahead_in_this_frame_stops_at_or_just_after_it()
    {
        var c64 = Build();
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        c64.Vic2.AdvanceRaster(10 * cyclesPerLine);

        var condition = c64.BuildRunUntilRasterCondition(100, 20);
        var result = RunUntil(c64, condition);

        Assert.True(result.Triggered);
        Assert.Equal(ExecEvaluatorTriggerReasonType.RunUntilCondition, result.TriggerType);
        Assert.Equal(0, Value(c64, "FRAME"));       // this frame
        Assert.Equal(100, Value(c64, "RASTER"));
        Assert.InRange(Value(c64, "CYCLE"), 20, 21);   // NOPs are 2 cycles: at the position or one cycle past it
    }

    [Fact]
    public void Run_until_a_raster_position_already_passed_stops_in_the_next_frame()
    {
        var c64 = Build();
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        c64.Vic2.AdvanceRaster(200 * cyclesPerLine);

        var condition = c64.BuildRunUntilRasterCondition(100);
        var result = RunUntil(c64, condition);

        Assert.True(result.Triggered);
        Assert.Equal(1, Value(c64, "FRAME"));       // the next frame
        Assert.Equal(100, Value(c64, "RASTER"));
        Assert.InRange(Value(c64, "CYCLE"), 1, 2);
    }

    [Fact]
    public void Run_until_the_current_position_waits_a_whole_frame()
    {
        var c64 = Build();
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        c64.Vic2.AdvanceRaster(100 * cyclesPerLine);

        var result = RunUntil(c64, c64.BuildRunUntilRasterCondition(100, 1));

        Assert.True(result.Triggered);
        Assert.Equal(1, Value(c64, "FRAME"));
        Assert.Equal(100, Value(c64, "RASTER"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Run_until_the_last_cycle_of_a_line_is_not_skipped_by_an_instruction_straddling_the_line(int startOffset)
    {
        // The target is the last cycle of line 100. Depending on the instruction boundaries the
        // stop lands on it or, when an instruction straddles the line end, in line 101; the
        // condition is on the frame's cycle count, so it stops at once either way, not a frame
        // later as a check of the line and cycle alone would.
        var c64 = Build();
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        c64.Vic2.AdvanceRaster(10 * cyclesPerLine + (ulong)startOffset);

        var result = RunUntil(c64, c64.BuildRunUntilRasterCondition(100, 63));

        Assert.True(result.Triggered);
        Assert.Equal(0, Value(c64, "FRAME"));
        var target = 100 * 63 + 62;
        Assert.InRange(Value(c64, "FRAMECYCLE"), target, target + 2);   // the longest instruction here is the 3-cycle JMP
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(312, 1)]
    [InlineData(100, 0)]
    [InlineData(100, 64)]
    public void A_raster_position_outside_the_model_is_rejected(int line, int cycle)
    {
        var c64 = Build();
        Assert.Throws<ArgumentException>(() => c64.BuildRunUntilRasterCondition(line, cycle));
    }

    [Fact]
    public void Ntsc_has_65_cycles_per_line_and_263_lines()
    {
        var c64 = Build("NTSC");
        c64.Vic2.AdvanceRaster(65 * 3 + 64);
        Assert.Equal(3, Value(c64, "RASTER"));
        Assert.Equal(65, Value(c64, "CYCLE"));
        c64.BuildRunUntilRasterCondition(262, 65);   // the last position is accepted
        Assert.Throws<ArgumentException>(() => c64.BuildRunUntilRasterCondition(263, 1));
    }

    [Fact]
    public void The_raster_run_target_parses_its_arguments()
    {
        var c64 = Build();
        Assert.True(c64.TryBuildRunUntilCondition("raster", ["100", "20"], out var condition));
        Assert.Equal(c64.BuildRunUntilRasterCondition(100, 20), condition);
        Assert.True(c64.TryBuildRunUntilCondition("RASTER", ["100"], out condition));
        Assert.Equal(c64.BuildRunUntilRasterCondition(100), condition);
        Assert.False(c64.TryBuildRunUntilCondition("sprite", ["1"], out _));
        Assert.Throws<ArgumentException>(() => c64.TryBuildRunUntilCondition("raster", [], out _));
        Assert.Throws<ArgumentException>(() => c64.TryBuildRunUntilCondition("raster", ["abc"], out _));
        Assert.Throws<ArgumentException>(() => c64.TryBuildRunUntilCondition("raster", ["100", "x"], out _));
        Assert.Throws<ArgumentException>(() => c64.TryBuildRunUntilCondition("raster", ["1", "2", "3"], out _));
    }
}
