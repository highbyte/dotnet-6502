using System.ComponentModel.DataAnnotations;
using System.Globalization;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class RegisterCommands
{
    public static CommandLineApplication ConfigureRegisters(this CommandLineApplication app, MonitorBase monitor, MonitorVariables monitorVariables)
    {
        app.Command("r", cmd =>
        {
            cmd.HelpOption(inherited: true);
            cmd.Description = "Show processor status and registers. CY = #cycles executed.";
            cmd.AddName("reg");

            cmd.Command("a", setRegisterCmd =>
                {
                    setRegisterCmd.Description = "Sets A register.";
                    var regVal = setRegisterCmd.Argument("value", "Value of A register (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return monitor.WriteValidationError(validationResult);
                    });

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        monitor.Cpu.A = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        monitor.WriteOutput($"{OutputGen.GetRegisters(monitor.Cpu)}");
                        return (int)CommandResult.Ok;
                    });
                });

            cmd.Command("x", setRegisterCmd =>
                {
                    setRegisterCmd.Description = "Sets X register.";
                    var regVal = setRegisterCmd.Argument("value", "Value of X register (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return monitor.WriteValidationError(validationResult);
                    });

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        monitor.Cpu.X = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        monitor.WriteOutput($"{OutputGen.GetRegisters(monitor.Cpu)}");
                        return (int)CommandResult.Ok;
                    });
                });

            cmd.Command("y", setRegisterCmd =>
                {
                    setRegisterCmd.Description = "Sets Y register.";
                    var regVal = setRegisterCmd.Argument("value", "Value of Y register (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return monitor.WriteValidationError(validationResult);
                    });

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        monitor.Cpu.Y = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        monitor.WriteOutput($"{OutputGen.GetRegisters(monitor.Cpu)}");
                        return (int)CommandResult.Ok;
                    });
                });

            cmd.Command("sp", setRegisterCmd =>
                {
                    setRegisterCmd.Description = "Sets SP (Stack Pointer).";
                    var regVal = setRegisterCmd.Argument("value", "Value of SP (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return monitor.WriteValidationError(validationResult);
                    });

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        monitor.Cpu.SP = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        monitor.WriteOutput($"{OutputGen.GetPCandSP(monitor.Cpu)}");
                        return (int)CommandResult.Ok;
                    });
                });

            cmd.Command("ps", setRegisterCmd =>
                {
                    setRegisterCmd.Description = "Sets processor status register.";
                    var regVal = setRegisterCmd.Argument("value", "Value of processor status register (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return monitor.WriteValidationError(validationResult);
                    });

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        monitor.Cpu.ProcessorStatus.Value = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        monitor.WriteOutput($"PS={value}");
                        monitor.WriteOutput($"{OutputGen.GetStatus(monitor.Cpu)}");
                        return (int)CommandResult.Ok;
                    });
                });

            cmd.Command("pc", setRegisterCmd =>
                {
                    setRegisterCmd.Description = "Sets PC (Program Counter).";
                    var regVal = setRegisterCmd.Argument("value", "Value of PC (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe16BitHexValueValidator());

                    setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return monitor.WriteValidationError(validationResult);
                    });

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        monitor.Cpu.PC = ushort.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        monitor.WriteOutput($"{OutputGen.GetPCandSP(monitor.Cpu)}");
                        return (int)CommandResult.Ok;
                    });
                });

            cmd.OnValidationError((ValidationResult validationResult) =>
            {
                return monitor.WriteValidationError(validationResult);
            });

            cmd.OnExecute(() =>
            {
                monitor.WriteOutput(OutputGen.GetProcessorState(monitor.Cpu, includeCycles: true));
                return (int)CommandResult.Ok;
            });
        });

        return app;
    }
}