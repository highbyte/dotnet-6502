using System.CommandLine;
using System.Globalization;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class ExecutionCommands
{
    public static Command ConfigureExecution(this Command rootCommand, MonitorBase monitor, MonitorVariables monitorVariables)
    {
        rootCommand.AddCommand(BuildGoCommand(monitor));
        rootCommand.AddCommand(BuildSingleStepCommand(monitor));
        return rootCommand;
    }

    private static Command BuildGoCommand(MonitorBase monitor)
    {
        var addressArg = new Argument<string>()
        {
            Name = "address",
            Description = "Optional address (hex) to start executing code at.",
            Arity = ArgumentArity.ZeroOrOne
        }
        .MustBe16BitHex();

        var command = new Command("g", "Change the PC (Program Counter) to the specified address continue execution.")
        {
            addressArg,
        };

        Func<string, Task<int>> handler = (string address) =>
        {
            if (string.IsNullOrEmpty(address))
                return Task.FromResult((int)CommandResult.Continue);

            monitor.Cpu.PC = ushort.Parse(address, NumberStyles.AllowHexSpecifier, null);
            //monitor.WriteOutput($"Starting executing code at {monitor.Cpu.PC.ToHex("", lowerCase: true)}");

            return Task.FromResult((int)CommandResult.Continue);

        };

        command.SetHandler(handler, addressArg);
        return command;
    }

    private static Command BuildSingleStepCommand(MonitorBase monitor)
    {
        var insCountArg = new Argument<ulong>(() => 1)
        {
            Name = "inscount",
            Description = "Number of instructions to execute. Defaults to 1.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var command = new Command("z", "Single step through instructions. Optionally execute a specified number of instructions.")
        {
            insCountArg
        };

        Func<ulong, Task<int>> handler = (ulong inscount) =>
        {
            //monitor.WriteOutput($"Executing code at {monitor.Cpu.PC.ToHex("",lowerCase:true)} for {inscount.Value} instruction(s).");
            monitor.Cpu.Execute(monitor.Mem, LegacyExecEvaluator.InstructionCountExecEvaluator(inscount));
            // Last instruction
            // monitor.WriteOutput($"{OutputGen.GetLastInstructionDisassembly(monitor.Cpu, monitor.Mem)}");
            // Next instruction
            monitor.WriteOutput($"{OutputGen.GetNextInstructionDisassembly(monitor.Cpu, monitor.Mem)}");

            return Task.FromResult((int)CommandResult.Ok);

        };

        command.SetHandler(handler, insCountArg);
        return command;
    }

}
