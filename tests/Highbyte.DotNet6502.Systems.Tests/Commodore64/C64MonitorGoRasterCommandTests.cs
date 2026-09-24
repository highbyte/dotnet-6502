using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

/// <summary>
/// The C64 monitor's 'gr' command: continue until the VIC-II reaches a raster line and cycle.
/// </summary>
public class C64MonitorGoRasterCommandTests
{
    private sealed class CapturingMonitor : MonitorBase
    {
        public List<string> Output { get; } = new();
        public CapturingMonitor(SystemRunner systemRunner) : base(systemRunner, new MonitorConfig()) { }
        public override void WriteOutput(string message) => Output.Add(message);
        public override void WriteOutput(string message, MessageSeverity severity) => Output.Add(message);
        public override bool LoadBinary(string fileName, out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null)
            => throw new NotImplementedException();
        public override bool LoadBinary(out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null)
            => throw new NotImplementedException();
        public override void SaveBinary(string fileName, ushort startAddress, ushort endAddress, bool addFileHeaderWithLoadAddress)
            => throw new NotImplementedException();
    }

    private static (C64 c64, CapturingMonitor monitor) Build()
    {
        var c64 = C64.BuildC64(new C64Config { LoadROMs = false, C64Model = "C64PAL", Vic2Model = "PAL" }, NullLoggerFactory.Instance);
        for (var address = 0x1000; address < 0x1100; address++)
            c64.Mem[(ushort)address] = 0xEA;
        c64.Mem.StoreData(0x1100, [0x4C, 0x00, 0x10]);
        c64.CPU.PC = 0x1000;
        var runner = new SystemRunner(c64);
        return (c64, new CapturingMonitor(runner));
    }

    [Fact]
    public void Go_raster_continues_until_the_position_and_the_monitor_shows_it()
    {
        var (c64, monitor) = Build();

        var result = monitor.SendCommand("gr 100 20");

        Assert.Equal(CommandResult.Continue, result);
        Assert.Equal(c64.BuildRunUntilRasterCondition(100, 20), monitor.Evaluator.RunUntilCondition);

        // Run as a host would: frames with the monitor's evaluator, until it triggers.
        ExecEvaluatorTriggerResult trigger = ExecEvaluatorTriggerResult.NotTriggered;
        for (var frame = 0; frame < 3 && !trigger.Triggered; frame++)
            trigger = c64.ExecuteOneFrame(monitor.Evaluator);
        Assert.True(trigger.Triggered);
        Assert.Equal(ExecEvaluatorTriggerReasonType.RunUntilCondition, trigger.TriggerType);

        monitor.Reset();
        monitor.ShowInfoAfterBreakTriggerEnabled(trigger);
        monitor.SendCommand("r");
        Assert.StartsWith("Condition '", monitor.Output[0]);
        Assert.Matches(@"^VIC-II: RASTER=100 CYCLE=2[01] FRAMECYCLE=\d+ FRAME=0$", monitor.Output[2]);
    }

    [Fact]
    public void Go_raster_defaults_the_cycle_to_the_lines_first()
    {
        var (c64, monitor) = Build();
        Assert.Equal(CommandResult.Continue, monitor.SendCommand("gr 200"));
        Assert.Equal(c64.BuildRunUntilRasterCondition(200, 1), monitor.Evaluator.RunUntilCondition);
    }

    [Theory]
    [InlineData("gr 312", "Raster line must be 0 to 311")]
    [InlineData("gr 100 64", "Cycle must be 1 to 63")]
    public void Go_raster_rejects_a_position_outside_the_model(string command, string expectedMessage)
    {
        var (_, monitor) = Build();
        Assert.Equal(CommandResult.Error, monitor.SendCommand(command));
        Assert.Null(monitor.Evaluator.RunUntilCondition);
        Assert.StartsWith(expectedMessage, monitor.Output[0]);
        Assert.DoesNotContain("Parameter", monitor.Output[0]);
    }
}
