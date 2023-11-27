using System.CommandLine;
using System.Globalization;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class RegisterCommands
{
    public static Command ConfigureRegisters(this Command rootCommand, MonitorBase monitor, MonitorVariables monitorVariables)
    {
        rootCommand.AddCommand(BuildRegistersCommand(monitor, monitorVariables));
        return rootCommand;
    }

    private static Command BuildRegistersCommand(MonitorBase monitor, MonitorVariables monitorVariables)
    {

        // r a
        var regAArg = new Argument<string>()
        {
            Name = "value",
            Description = "Value of A register (hex).",
            Arity = ArgumentArity.ExactlyOne
        }
        .MustBe8BitHex();

        var setRegACommand = new Command("a", "Sets A register.")
        {
            regAArg
        };
        setRegACommand.SetHandler((string reg) =>
        {
            monitor.Cpu.A = byte.Parse(reg, NumberStyles.AllowHexSpecifier, null);
            monitor.WriteOutput($"{OutputGen.GetRegisters(monitor.Cpu)}");
        }, regAArg);


        // r x
        var regXArg = new Argument<string>()
        {
            Name = "value",
            Description = "Value of X register (hex).",
            Arity = ArgumentArity.ExactlyOne
        }
        .MustBe8BitHex();

        var setRegXCommand = new Command("x", "Sets X register.")
        {
            regXArg
        };
        setRegXCommand.SetHandler((string reg) =>
        {
            monitor.Cpu.X = byte.Parse(reg, NumberStyles.AllowHexSpecifier, null);
            monitor.WriteOutput($"{OutputGen.GetRegisters(monitor.Cpu)}");
        }, regXArg);

        // r y
        var regYArg = new Argument<string>()
        {
            Name = "value",
            Description = "Value of Y register (hex).",
            Arity = ArgumentArity.ExactlyOne
        }
        .MustBe8BitHex();

        var setRegYCommand = new Command("y", "Sets Y register.")
        {
            regYArg
        };
        setRegYCommand.SetHandler((string reg) =>
        {
            monitor.Cpu.Y = byte.Parse(reg, NumberStyles.AllowHexSpecifier, null);
            monitor.WriteOutput($"{OutputGen.GetRegisters(monitor.Cpu)}");
        }, regYArg);

        // r sp
        var regSPArg = new Argument<string>()
        {
            Name = "value",
            Description = "Value of SP register (hex).",
            Arity = ArgumentArity.ExactlyOne
        }
        .MustBe8BitHex();

        var setRegSPCommand = new Command("sp", "Sets SP register.")
        {
            regSPArg
        };
        setRegSPCommand.SetHandler((string reg) =>
        {
            monitor.Cpu.SP = byte.Parse(reg, NumberStyles.AllowHexSpecifier, null);
            monitor.WriteOutput($"{OutputGen.GetPCandSP(monitor.Cpu)}");
        }, regSPArg);

        // r ps
        var regPSArg = new Argument<string>()
        {
            Name = "value",
            Description = "Value of PS register (hex).",
            Arity = ArgumentArity.ExactlyOne
        }
        .MustBe8BitHex();

        var setRegPSCommand = new Command("ps", "Sets PS register.")
        {
            regPSArg
        };
        setRegPSCommand.SetHandler((string reg) =>
        {
            monitor.Cpu.ProcessorStatus.Value = byte.Parse(reg, NumberStyles.AllowHexSpecifier, null);
            monitor.WriteOutput($"{OutputGen.GetStatus(monitor.Cpu)}");
        }, regPSArg);

        // r pc
        var regPCArg = new Argument<string>()
        {
            Name = "value",
            Description = "Value of PC register (hex).",
            Arity = ArgumentArity.ExactlyOne
        }
        .MustBe16BitHex();

        var setRegPCCommand = new Command("pc", "Sets PC register.")
        {
            regPCArg
        };
        setRegPCCommand.SetHandler((string reg) =>
        {
            monitor.Cpu.PC = ushort.Parse(reg, NumberStyles.AllowHexSpecifier, null);
            monitor.WriteOutput($"{OutputGen.GetPCandSP(monitor.Cpu)}");
        }, regPCArg);

        // r
        var command = new Command("r", "Show processor status and registers. CY = #cycles executed.")
        {
            setRegACommand,
            setRegXCommand,
            setRegYCommand,
            setRegSPCommand,
            setRegPSCommand,
            setRegPCCommand
        };
        command.AddAlias("reg");
        command.SetHandler(() =>
        {
            monitor.WriteOutput(OutputGen.GetProcessorState(monitor.Cpu, includeCycles: true));
        });

        return command;
    }
}
