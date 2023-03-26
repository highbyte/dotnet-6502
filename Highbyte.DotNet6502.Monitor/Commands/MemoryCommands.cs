using System.ComponentModel.DataAnnotations;
using System.Globalization;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class MemoryCommands
{
    public static CommandLineApplication ConfigureMemory(this CommandLineApplication app, MonitorBase monitor, MonitorVariables monitorVariables)
    {
        app.Command("m", cmd =>
        {
            cmd.HelpOption(inherited: true);
            cmd.Description = "Show contents of emulator memory in bytes.";
            cmd.AddName("mem");

            var start = cmd.Argument("start", "Start address (hex). If not specified, the 0000 address is used.");
            start.Validators.Add(new MustBe16BitHexValueValidator());

            var end = cmd.Argument("end", "End address (hex). If not specified, a default number of memory locations will be shown from start.");
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
                    if (!monitorVariables.LatestMemoryDumpAddress.HasValue)
                        monitorVariables.LatestMemoryDumpAddress = 0x0000;
                    startAddress = monitorVariables.LatestMemoryDumpAddress.Value;
                }
                else
                {
                    startAddress = ushort.Parse(start.Value, NumberStyles.AllowHexSpecifier, null);
                }

                ushort endAddress;
                if (string.IsNullOrEmpty(end.Value))
                {
                    const int DEFAULT_BYTES_TO_SHOW = (16 * 8);
                    ushort endAddressDelta = DEFAULT_BYTES_TO_SHOW - 1;
                    if ((uint)((uint)startAddress + (uint)endAddressDelta) <= 0xffff)
                        endAddress = (ushort)(startAddress + endAddressDelta);
                    else
                        endAddress = 0xffff;
                }
                else
                {
                    endAddress = ushort.Parse(end.Value, NumberStyles.AllowHexSpecifier, null);
                    if (endAddress < startAddress)
                        endAddress = startAddress;
                }

                var list = OutputMemoryGen.GetFormattedMemoryList(monitor.Mem, startAddress, endAddress);
                foreach (var line in list)
                    monitor.WriteOutput(line);

                monitorVariables.LatestMemoryDumpAddress = ++endAddress;

                return (int)CommandResult.Ok;
            });
        });

        app.Command("f", cmd =>
        {
            cmd.HelpOption(inherited: true);
            cmd.Description = $"Fill memory at specified address with a list of bytes.{Environment.NewLine}  Example: f 1000 20 ff ab 30";
            cmd.AddName("fill");

            var memAddress = cmd.Argument("address", "Memory address (hex).").IsRequired();
            memAddress.Validators.Add(new MustBe16BitHexValueValidator());

            var memValues = cmd.Argument("values", "List of byte values (hex). Example: 20 ff ab 30").IsRequired();
            memValues.MultipleValues = true;
            memValues.Validators.Add(new MustBe8BitHexValueValidator());

            cmd.OnValidationError((ValidationResult validationResult) =>
            {
                return monitor.WriteValidationError(validationResult);
            });

            cmd.OnExecute(() =>
            {
                var address = ushort.Parse(memAddress.Value, NumberStyles.AllowHexSpecifier, null);
                List<byte> bytes = new();
                foreach (var val in memValues.Values)
                    bytes.Add(byte.Parse(val, NumberStyles.AllowHexSpecifier, null));
                foreach (var val in bytes)
                    monitor.Mem[address++] = val;
                return (int)CommandResult.Ok;
            });
        });

        return app;
    }
}
