using System.ComponentModel.DataAnnotations;
using System.Globalization;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor.Commands
{
    /// <summary>
    /// </summary>
    public static class DisassemblyCommands
    {
        public static CommandLineApplication ConfigureDisassembly(this CommandLineApplication app, MonitorBase monitor)
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
                        startAddress = monitor.Cpu.PC;
                    else
                        startAddress = ushort.Parse(start.Value, NumberStyles.AllowHexSpecifier, null);

                    ushort endAddress;
                    if (string.IsNullOrEmpty(end.Value))
                    {
                        const int DEFAULT_BYTES_TO_SHOW = 0x10;
                        var endAddressDelta = DEFAULT_BYTES_TO_SHOW - 1;
                        if ((uint)((uint)startAddress + (uint)endAddressDelta) <= 0xffff)
                            endAddress = (ushort)(startAddress + endAddressDelta);
                        else
                            endAddress = 0xffff;
                        endAddress = (ushort)(startAddress + endAddressDelta);
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
                        var opCodeByte = monitor.Mem[currentAddress];
                        int insSize;
                        if (!monitor.Cpu.InstructionList.OpCodeDictionary.ContainsKey(opCodeByte))
                            insSize = 1;
                        else
                            insSize = monitor.Cpu.InstructionList.GetOpCode(opCodeByte).Size;

                        if (currentAddress < endAddress && ((uint)(currentAddress + insSize) <= 0xffff))
                            currentAddress += (ushort)insSize;
                        else
                            cont = false;
                    }
                    return (int)CommandResult.Ok;
                });
            });

            return app;
        }
    }
}