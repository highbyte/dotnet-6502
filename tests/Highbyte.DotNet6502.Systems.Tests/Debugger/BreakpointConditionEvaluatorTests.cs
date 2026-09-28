using Highbyte.DotNet6502.Systems.Debugger;

namespace Highbyte.DotNet6502.Systems.Tests.Debugger;

/// <summary>
/// The breakpoint condition syntax: registers, flags, memory, literals, the logical operators'
/// precedence, and the values an <see cref="IDebugValueSource"/> adds.
/// </summary>
public class BreakpointConditionEvaluatorTests
{
    /// <summary>A stand-in system with two values, as a C64 exposes its raster position.</summary>
    private sealed class TestValues : IDebugValueSource
    {
        public long Raster { get; set; }
        public long Big { get; set; } = 5_000_000_000L;   // beyond int.MaxValue

        public string DebugValueGroupName => "Test";
        public IReadOnlyList<DebugValueInfo> DebugValues { get; } = [new("RASTER", "line"), new("BIG", "big")];
        public IReadOnlyList<DebugValueInfo> RunUntilTargets => [];

        public bool TryGetDebugValue(string name, out long value)
        {
            switch (name.ToUpperInvariant())
            {
                case "RASTER": value = Raster; return true;
                case "BIG": value = Big; return true;
                default: value = 0; return false;
            }
        }

        public bool TryBuildRunUntilCondition(string target, IReadOnlyList<string> arguments, out string condition)
        {
            condition = "";
            return false;
        }
    }

    private static (CPU cpu, Memory mem) State()
    {
        var cpu = new CPU { A = 0xFF, X = 10, Y = 3, SP = 0xFD, PC = 0xC080 };
        cpu.ProcessorStatus.Zero = true;
        var mem = new Memory();
        mem[0xD020] = 0x01;
        mem[0x030A] = 0x80;
        return (cpu, mem);
    }

    [Theory]
    [InlineData("A == $FF", true)]
    [InlineData("a == $ff", true)]
    [InlineData("X >= 10", true)]
    [InlineData("X > 10", false)]
    [InlineData("Z == 1", true)]
    [InlineData("C == 0", true)]
    [InlineData("PC == $C080", true)]
    [InlineData("PC == 0xC080", true)]
    [InlineData("PC == 49280", true)]
    [InlineData("[$D020] == $01", true)]
    [InlineData("[$0300 + X] > $7F", true)]
    [InlineData("A == $FF && X == 0", false)]
    [InlineData("A == $00 || A == $FF", true)]
    public void Registers_flags_memory_and_literals(string condition, bool expected)
    {
        var (cpu, mem) = State();
        Assert.Equal(expected, BreakpointConditionEvaluator.Evaluate(condition, cpu, mem));
    }

    [Fact]
    public void And_binds_tighter_than_or()
    {
        var (cpu, mem) = State();
        // (X == 0 && A == $FF) || Y == 3 — true through the right side only.
        Assert.True(BreakpointConditionEvaluator.Evaluate("X == 0 && A == $FF || Y == 3", cpu, mem));
        // Y == 3 || (X == 0 && A == $FF) — true through the left side only.
        Assert.True(BreakpointConditionEvaluator.Evaluate("Y == 3 || X == 0 && A == $FF", cpu, mem));
        // (Y == 3 && X == 0) || A == 0 — false.
        Assert.False(BreakpointConditionEvaluator.Evaluate("Y == 3 && X == 0 || A == 0", cpu, mem));
    }

    [Fact]
    public void An_unparsable_condition_stops_as_a_fail_safe()
    {
        var (cpu, mem) = State();
        Assert.True(BreakpointConditionEvaluator.Evaluate("FOO == 1", cpu, mem));
        Assert.True(BreakpointConditionEvaluator.Evaluate("A ==", cpu, mem));
        Assert.True(BreakpointConditionEvaluator.Evaluate("", cpu, mem));
    }

    [Fact]
    public void A_systems_values_are_operands_when_a_source_is_given()
    {
        var (cpu, mem) = State();
        var values = new TestValues { Raster = 100 };
        Assert.True(BreakpointConditionEvaluator.Evaluate("RASTER == 100", cpu, mem, values));
        Assert.True(BreakpointConditionEvaluator.Evaluate("raster >= 50 && A == $FF", cpu, mem, values));
        Assert.False(BreakpointConditionEvaluator.Evaluate("RASTER == 101", cpu, mem, values));
        // Registers still win over a value of the same name: A is the accumulator.
        Assert.True(BreakpointConditionEvaluator.Evaluate("A == $FF", cpu, mem, values));
    }

    [Fact]
    public void Values_beyond_32_bits_compare_correctly()
    {
        var (cpu, mem) = State();
        var values = new TestValues();
        Assert.True(BreakpointConditionEvaluator.Evaluate("BIG > 4000000000", cpu, mem, values));
        Assert.True(BreakpointConditionEvaluator.Evaluate("BIG == 5000000000", cpu, mem, values));
    }

    [Fact]
    public void A_value_name_the_system_does_not_expose_is_a_parse_error()
    {
        var (cpu, mem) = State();
        var values = new TestValues();
        Assert.False(BreakpointConditionEvaluator.TryParse("CYCLE == 1", cpu, mem, values, out var error));
        Assert.Contains("CYCLE", error);
        // and, as with any parse error, Evaluate stops as a fail-safe
        Assert.True(BreakpointConditionEvaluator.Evaluate("CYCLE == 1", cpu, mem, values));
    }

    [Theory]
    [InlineData("A == $FF", true, null)]
    [InlineData("RASTER == 100 && X > 1", true, null)]
    [InlineData("", false, "empty")]
    [InlineData("A ==", false, "Expected")]
    [InlineData("A == 1 foo", false, "Unexpected text")]
    [InlineData("FOO == 1", false, "FOO")]
    public void TryParse_reports_the_first_problem(string condition, bool ok, string? errorContains)
    {
        var (cpu, mem) = State();
        Assert.Equal(ok, BreakpointConditionEvaluator.TryParse(condition, cpu, mem, new TestValues(), out var error));
        if (errorContains == null)
            Assert.Null(error);
        else
            Assert.Contains(errorContains, error);
    }
}
