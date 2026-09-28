using Highbyte.DotNet6502.Monitor.Tests.Helpers;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Debugger;

namespace Highbyte.DotNet6502.Monitor.Tests;

/// <summary>
/// The 'gu' (go until a condition) command and the system's values on the 'r' output.
/// </summary>
public class RunUntilAndSystemValuesCommandTests
{
    private const byte OpCodeNop = 0xEA;

    /// <summary>A test system that, like a C64, exposes named values to debuggers.</summary>
    private sealed class TestSystemWithValues : TestSystem, IDebugValueSource
    {
        public long Raster { get; set; } = 51;
        public string DebugValueGroupName => "VIC-II";
        public IReadOnlyList<DebugValueInfo> DebugValues { get; } = [new("RASTER", "line"), new("CYCLE", "cycle")];
        public IReadOnlyList<DebugValueInfo> RunUntilTargets => [];
        public bool TryGetDebugValue(string name, out long value)
        {
            switch (name.ToUpperInvariant())
            {
                case "RASTER": value = Raster; return true;
                case "CYCLE": value = 12; return true;
                default: value = 0; return false;
            }
        }
        public bool TryBuildRunUntilCondition(string target, IReadOnlyList<string> arguments, out string condition)
        {
            condition = "";
            return false;
        }
    }

    private static TestMonitor CreateMonitor(ISystem system)
    {
        for (int address = 0; address <= 0xffff; address++)
            system.Mem[(ushort)address] = OpCodeNop;
        system.CPU.PC = 0x1000;
        return new TestMonitor(new SystemRunner(system), new MonitorConfig());
    }

    [Fact]
    public void Registers_show_the_systems_values_on_a_second_line()
    {
        var monitor = CreateMonitor(new TestSystemWithValues());
        monitor.SendCommand("r");
        Assert.Equal(2, monitor.Output.Count);
        Assert.Contains("PC=1000", monitor.Output[0]);
        Assert.Equal("VIC-II: RASTER=51 CYCLE=12", monitor.Output[1]);
    }

    [Fact]
    public void Registers_have_no_second_line_for_a_system_without_values()
    {
        var monitor = CreateMonitor(new TestSystem());
        monitor.SendCommand("r");
        Assert.Single(monitor.Output);
    }

    [Fact]
    public void Go_until_sets_the_condition_and_continues()
    {
        var monitor = CreateMonitor(new TestSystemWithValues());
        var result = monitor.SendCommand("gu RASTER == 100 && A == $FF");
        Assert.Equal(CommandResult.Continue, result);
        Assert.Equal("RASTER == 100 && A == $FF", monitor.Evaluator.RunUntilCondition);
        Assert.Empty(monitor.Output);
    }

    [Fact]
    public void Go_until_rejects_a_condition_that_does_not_parse()
    {
        var monitor = CreateMonitor(new TestSystemWithValues());
        var result = monitor.SendCommand("gu BORDER == 1");
        Assert.Equal(CommandResult.Error, result);
        Assert.Null(monitor.Evaluator.RunUntilCondition);
        Assert.Contains("BORDER", monitor.FirstOutputLine);
    }

    [Fact]
    public void Go_until_condition_can_use_the_systems_values_only_when_it_has_them()
    {
        var monitor = CreateMonitor(new TestSystem());
        Assert.Equal(CommandResult.Error, monitor.SendCommand("gu RASTER == 100"));
        Assert.Equal(CommandResult.Continue, monitor.SendCommand("gu A == 1"));
    }

    [Fact]
    public void Entering_the_monitor_again_drops_a_pending_condition()
    {
        var monitor = CreateMonitor(new TestSystemWithValues());
        monitor.SendCommand("gu A == 1");
        monitor.Reset();
        Assert.Null(monitor.Evaluator.RunUntilCondition);
    }

    [Fact]
    public void The_evaluator_reports_a_met_condition_on_entry()
    {
        var monitor = CreateMonitor(new TestSystemWithValues());
        var trigger = ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.RunUntilCondition, "Condition 'A == 1' met at $1000");
        monitor.ShowInfoAfterBreakTriggerEnabled(trigger);
        Assert.Equal("Condition 'A == 1' met at $1000", monitor.FirstOutputLine);
    }
}
