using System.CommandLine;
using System.Globalization;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class MemoryCommands
{
    public static Command ConfigureMemory(this Command rootCommand, MonitorBase monitor, MonitorVariables monitorVariables)
    {
        rootCommand.AddCommand(BuildMemoryDumpCommand(monitor, monitorVariables));
        rootCommand.AddCommand(BuildMemoryFillCommand(monitor, monitorVariables));
        return rootCommand;
    }

    private static Command BuildMemoryDumpCommand(MonitorBase monitor, MonitorVariables monitorVariables)
    {
        var startArg = new Argument<string>()
        {
            Name = "start",
            Description = "Start address (hex). If not specified, the 0000 address is used.",
            Arity = ArgumentArity.ZeroOrOne
        }
        .MustBe16BitHex();

        var endArg = new Argument<string>()
        {
            Name = "end",
            Description = "End address (hex). If not specified, a default number of memory locations will be shown from start.",
            Arity = ArgumentArity.ZeroOrOne
        }
        .MustBe16BitHex()
        .GreaterThan16bit(startArg);

        var command = new Command("m", "Disassembles 6502 code from emulator memory.")
        {
            startArg,
            endArg
        };

        command.SetHandler((string start, string end) =>
        {
            ushort startAddress;
            if (string.IsNullOrEmpty(start))
            {
                if (!monitorVariables.LatestMemoryDumpAddress.HasValue)
                    monitorVariables.LatestMemoryDumpAddress = 0x0000;
                startAddress = monitorVariables.LatestMemoryDumpAddress.Value;
            }
            else
            {
                startAddress = ushort.Parse(start, NumberStyles.AllowHexSpecifier, null);
            }

            ushort endAddress;
            if (string.IsNullOrEmpty(end))
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
                endAddress = ushort.Parse(end, NumberStyles.AllowHexSpecifier, null);
                if (endAddress < startAddress)
                    endAddress = startAddress;
            }

            var list = OutputMemoryGen.GetFormattedMemoryList(monitor.Mem, startAddress, endAddress);
            foreach (var line in list)
                monitor.WriteOutput(line);

            monitorVariables.LatestMemoryDumpAddress = ++endAddress;
        }, startArg, endArg);
        return command;
    }

    private static Command BuildMemoryFillCommand(MonitorBase monitor, MonitorVariables monitorVariables)
    {
        var addressArg = new Argument<string>()
        {
            Name = "address",
            Description = "Memory address (hex).",
            Arity = ArgumentArity.ExactlyOne
        }
        .MustBe16BitHex();

        var valuesArg = new Argument<string[]>()
        {
            Name = "values",
            Description = "List of byte values (hex). Example: 20 ff ab 30",
            Arity = ArgumentArity.OneOrMore
        }
        .MustBe8BitHex();

        var command = new Command("f", $"Fill memory at specified address with a list of bytes.{Environment.NewLine}Example: f 1000 20 ff ab 30")
        {
            addressArg,
            valuesArg
        };
        command.AddAlias("fill");

        command.SetHandler((string memAddress, string[] memValues) =>
        {
            var address = ushort.Parse(memAddress, NumberStyles.AllowHexSpecifier, null);
            List<byte> bytes = new();
            foreach (var val in memValues)
                bytes.Add(byte.Parse(val!, NumberStyles.AllowHexSpecifier, null));
            foreach (var val in bytes)
                monitor.Mem[address++] = val;

        }, addressArg, valuesArg);
        return command;
    }
}
