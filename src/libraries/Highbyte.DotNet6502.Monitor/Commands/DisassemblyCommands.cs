using System.CommandLine;
using System.Globalization;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class DisassemblyCommands
{
    public static Command ConfigureDisassembly(this Command rootCommand, MonitorBase monitor, MonitorVariables monitorVariables)
    {
        rootCommand.AddCommand(BuildDisassemblyCommand(monitor, monitorVariables));
        return rootCommand;
    }

    private static Command BuildDisassemblyCommand(MonitorBase monitor, MonitorVariables monitorVariables)
    {
        var startArg = new Argument<string>()
        {
            Name = "start",
            Description = "Start address (hex). If not specified, the current PC address is used.",
            Arity = ArgumentArity.ZeroOrOne
        }
        .MustBe16BitHex();

        var endArg = new Argument<string>()
        {
            Name = "end",
            Description = "End address (hex). If not specified, a default number of addresses will be shown from start.",
            Arity = ArgumentArity.ZeroOrOne
        }
        .MustBe16BitHex()
        .GreaterThan16bit(startArg);

        var command = new Command("d", "Disassembles 6502 code from emulator memory.")
        {
            startArg,
            endArg
        };

        command.SetHandler((string start, string end) =>
        {
            ushort startAddress;
            if (string.IsNullOrEmpty(start))
            {
                if (!monitorVariables.LatestDisassemblyAddress.HasValue)
                    monitorVariables.LatestDisassemblyAddress = monitor.Cpu.PC;
                startAddress = monitorVariables.LatestDisassemblyAddress.Value;
            }
            else
            {
                startAddress = ushort.Parse(start, NumberStyles.AllowHexSpecifier, null);
            }

            ushort? endAddress = null;
            int? instructionShowCount = null;
            if (string.IsNullOrEmpty(end))
            {
                instructionShowCount = 10;
            }
            else
            {
                endAddress = ushort.Parse(end, NumberStyles.AllowHexSpecifier, null);
                if (endAddress < startAddress)
                    endAddress = startAddress;
            }

            ushort currentAddress = startAddress;
            bool cont = true;
            while (cont)
            {
                monitor.WriteOutput(OutputGen.GetInstructionDisassembly(monitor.Cpu, monitor.Mem, currentAddress));
                var nextInstructionAddress = monitor.Cpu.GetNextInstructionAddress(monitor.Mem, currentAddress);

                if (instructionShowCount.HasValue)
                {
                    instructionShowCount--;
                    if (instructionShowCount == 0)
                        cont = false;
                }
                else
                {
                    if (nextInstructionAddress > endAddress || (currentAddress >= nextInstructionAddress))
                        cont = false;
                }
                currentAddress = nextInstructionAddress;
            }

            monitorVariables.LatestDisassemblyAddress = currentAddress;
        }, startArg, endArg);
        return command;
    }
}
