using System.CommandLine;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class ResetCommands
{
    public static Command ConfigureReset(this Command rootCommand, MonitorBase monitor, MonitorVariables monitorVariables)
    {
        rootCommand.AddCommand(BuildResetCommand(monitor, monitorVariables));
        return rootCommand;
    }

    private static Command BuildResetCommand(MonitorBase monitor, MonitorVariables monitorVariables)
    {
        // r
        var command = new Command("reset", "Resets the computer (soft, memory intact).")
        {
        };
        command.SetHandler(() =>
        {
            monitor.Cpu.Reset(monitor.Mem); // A soft reset, memory not cleared.
            return Task.FromResult((int)CommandResult.Continue);
        });

        return command;
    }
}
