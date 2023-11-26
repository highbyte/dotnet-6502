using System.CommandLine;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class OptionCommands
{
    public static Command ConfigureOptions(this Command rootCommand, MonitorBase monitor, MonitorVariables monitorVariables)
    {
        rootCommand.AddCommand(BuildOptionsCommand(monitor, monitorVariables));
        return rootCommand;
    }

    private static Command BuildOptionsCommand(MonitorBase monitor, MonitorVariables monitorVariables)
    {

        // o u
        var uValArg = new Argument<string>()
        {
            Name = "flag",
            Description = "Unknown instruction flag",
            Arity = ArgumentArity.ExactlyOne
        }
        .MustBeIntegerFlag();

        var unknownInsCommand = new Command("u", "Flag how to handle unknown instructions (0 = continue, 1 = stop).")
        {
            uValArg
        };
        unknownInsCommand.SetHandler((string uVal) =>
        {
            var value = uVal;
            monitor.Options.StopAfterUnknownInstruction = value == "1";
            monitor.ApplyOptionsOnBreakPointExecEvaluator();
            monitor.ShowOptions();
        }, uValArg);


        // o b
        var bValArg = new Argument<string>()
        {
            Name = "flag",
            Description = "BRK instruction flag",
            Arity = ArgumentArity.ExactlyOne
        }
        .MustBeIntegerFlag();

        var bpCommand = new Command("b", "Flag how to handle BRK instruction (0 = continue, 1 = stop).")
        {
            bValArg
        };
        bpCommand.SetHandler((string bVal) =>
        {
            var value = bVal;
            monitor.Options.StopAfterBRKInstruction = value == "1";
            monitor.ApplyOptionsOnBreakPointExecEvaluator();
            monitor.ShowOptions();
        }, bValArg);


        // o
        var command = new Command("o", "Show global options.")
        {
            unknownInsCommand,
            bpCommand
        };
        command.AddAlias("options");
        command.SetHandler(() =>
        {
            monitor.ShowOptions();
        });

        return command;
    }
}
