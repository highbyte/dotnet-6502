using Highbyte.DotNet6502.Systems.Debugger;

namespace Highbyte.DotNet6502.Systems.Tests.Debugger;

/// <summary>
/// <see cref="DebuggerBreakpointEvaluator.RunUntilCondition"/>: checked before every instruction
/// regardless of address, stops once and clears itself.
/// </summary>
public class DebuggerBreakpointEvaluatorRunUntilTests
{
    [Fact]
    public void Stops_at_the_first_check_where_the_condition_holds_and_clears_itself()
    {
        var cpu = new CPU { PC = 0x1000, X = 0 };
        var mem = new Memory();
        ExecEvaluatorTriggerResult? seen = null;
        var evaluator = new DebuggerBreakpointEvaluator
        {
            RunUntilCondition = "X == 2",
            OnTriggered = (result, _, _) => seen = result,
        };

        Assert.False(evaluator.ShouldBreak(cpu, mem).Triggered);
        cpu.X = 1;
        Assert.False(evaluator.ShouldBreak(cpu, mem).Triggered);
        cpu.X = 2;
        cpu.PC = 0x2000;   // any address

        var trigger = evaluator.ShouldBreak(cpu, mem);

        Assert.True(trigger.Triggered);
        Assert.Equal(ExecEvaluatorTriggerReasonType.RunUntilCondition, trigger.TriggerType);
        Assert.Contains("X == 2", trigger.TriggerDescription);
        Assert.Contains("$2000", trigger.TriggerDescription);
        Assert.Same(trigger, seen);
        Assert.Null(evaluator.RunUntilCondition);
        Assert.False(evaluator.ShouldBreak(cpu, mem).Triggered);   // once only
    }

    [Fact]
    public void The_resume_skip_applies_to_the_run_until_check_too()
    {
        var cpu = new CPU { X = 2 };
        var mem = new Memory();
        var evaluator = new DebuggerBreakpointEvaluator
        {
            RunUntilCondition = "X == 2",
            SkipNextBreakpointCheck = true,
        };
        Assert.False(evaluator.ShouldBreak(cpu, mem).Triggered);
        Assert.True(evaluator.ShouldBreak(cpu, mem).Triggered);
    }

    [Fact]
    public void Breakpoint_conditions_see_the_systems_values()
    {
        var cpu = new CPU { PC = 0x1000 };
        var mem = new Memory();
        var values = new StubValues { Value = 10 };
        var evaluator = new DebuggerBreakpointEvaluator { DebugValues = values };
        evaluator.InstructionBreakpoints.Add(0x1000);
        evaluator.BreakpointConditions[0x1000] = "VALUE == 20";

        Assert.False(evaluator.ShouldBreak(cpu, mem).Triggered);
        values.Value = 20;
        Assert.True(evaluator.ShouldBreak(cpu, mem).Triggered);
    }

    private sealed class StubValues : IDebugValueSource
    {
        public long Value { get; set; }
        public string DebugValueGroupName => "Stub";
        public IReadOnlyList<DebugValueInfo> DebugValues { get; } = [new("VALUE", "a value")];
        public IReadOnlyList<DebugValueInfo> RunUntilTargets => [];
        public bool TryGetDebugValue(string name, out long value)
        {
            value = Value;
            return name.Equals("VALUE", StringComparison.OrdinalIgnoreCase);
        }
        public bool TryBuildRunUntilCondition(string target, IReadOnlyList<string> arguments, out string condition)
        {
            condition = "";
            return false;
        }
    }
}
