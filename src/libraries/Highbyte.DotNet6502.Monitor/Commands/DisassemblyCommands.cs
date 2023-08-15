using System.ComponentModel.DataAnnotations;
using System.Globalization;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class DisassemblyCommands
{
    public static CommandLineApplication ConfigureDisassembly(this CommandLineApplication app, MonitorBase monitor, MonitorVariables monitorVariables)
    {
        app.Command("d", cmd =>
        {
            cmd.HelpOption(inherited: true);
            cmd.Description = "Disassembles 6502 code from emulator memory.";

            var start = cmd.Argument("start", "Start address (hex). If not specified, the current PC address is used.");
            start.Validators.Add(new MustBe16BitHexValueValidator());

            var end = cmd.Argument("end", "End address (hex). If not specified, a default number of addresses will be shown from start.");
            end.Validators.Add(new MustBe16BitHexValueValidator());
            end.Validators.Add(new GreaterThan16bitValidator(start));

            cmd.OnValidationError((ValidationResult validationResult) =>
            {
                return monitor.WriteValidationError(validationResult);
            });

            cmd.OnExecute(() =>
            {
                ushort startAddress;
                if (string.IsNullOrEmpty(start.Value))
                {
                    if (!monitorVariables.LatestDisassemblyAddress.HasValue)
                        monitorVariables.LatestDisassemblyAddress = monitor.Cpu.PC;
                    startAddress = monitorVariables.LatestDisassemblyAddress.Value;
                }
                else
                {
                    startAddress = ushort.Parse(start.Value, NumberStyles.AllowHexSpecifier, null);
                }

                ushort? endAddress = null;
                int? instructionShowCount = null;
                if (string.IsNullOrEmpty(end.Value))
                {
                    instructionShowCount = 10;
                }
                else
                {
                    endAddress = ushort.Parse(end.Value, NumberStyles.AllowHexSpecifier, null);
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

                return (int)CommandResult.Ok;
            });
        });

        return app;
    }
}