using System.CommandLine;
using System.Globalization;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class BreakpointCommands
{
    public static Command ConfigureBreakpoints(this Command rootCommand, MonitorBase monitor, MonitorVariables monitorVariables)
    {
        rootCommand.AddCommand(BuildBreakpointCommand(monitor, monitorVariables));
        return rootCommand;
    }

    private static Command BuildBreakpointCommand(MonitorBase monitor, MonitorVariables monitorVariables)
    {

        // b l
        var listSubCommand = new Command("l", "Lists all breakpoints.")
        {
        };
        listSubCommand.SetHandler(() =>
        {
            return ListBreakpoints(monitor);
        });

        // b a
        var addressArg = new Argument<string>()
        {
            Name = "address",
            Description = "Memory address 16 bits (hex).",
            Arity = ArgumentArity.ExactlyOne
        }
        .MustBe16BitHex();

        var addSubCommand = new Command("a", "Add a breakpoint.")
        {
            addressArg
        };
        addSubCommand.SetHandler((string memAddress) =>
        {
            var address = ushort.Parse(memAddress, NumberStyles.AllowHexSpecifier, null);
            monitor.Evaluator.InstructionBreakpoints.Add(address);
        }, addressArg);

        // b d
        var addressDelArg = new Argument<string>()
        {
            Name = "address",
            Description = "Memory address 16 bits (hex).",
            Arity = ArgumentArity.ExactlyOne
        }
        .MustBe16BitHex();

        var delSubCommand = new Command("d", "Delete a breakpoint.")
        {
            addressDelArg
        };
        delSubCommand.SetHandler((string memAddress) =>
        {
            var address = ushort.Parse(memAddress, NumberStyles.AllowHexSpecifier, null);
            monitor.Evaluator.InstructionBreakpoints.Remove(address);
        }, addressDelArg);

        // b da
        var delAllSubCommand = new Command("da", "Delete all breakpoints.")
        {
        };
        delAllSubCommand.SetHandler(() =>
        {
            monitor.Evaluator.InstructionBreakpoints.Clear();
        });

        // b
        var command = new Command("b", "Breakpoints.")
        {
            listSubCommand,
            addSubCommand,
            delSubCommand,
            delAllSubCommand
        };
        command.AddAlias("bp");
        // Default command for just "b" without any subcommand
        command.SetHandler(() =>
        {
            return ListBreakpoints(monitor);
        });

        return command;
    }

    private static Task<int> ListBreakpoints(MonitorBase monitor)
    {
        if (monitor.Evaluator.InstructionBreakpoints.Count == 0)
            monitor.WriteOutput($"No breakpoints.");
        else
            monitor.WriteOutput($"Breakpoints:");

        foreach (var addr in monitor.Evaluator.InstructionBreakpoints)
        {
            monitor.WriteOutput($"{addr.ToHex()} : Enabled");
        }
        return Task.FromResult((int)CommandResult.Ok);
    }
}
