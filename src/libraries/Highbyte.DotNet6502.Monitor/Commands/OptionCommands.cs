using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class OptionCommands
{
    public static CommandLineApplication ConfigureOptions(this CommandLineApplication app, MonitorBase monitor, MonitorVariables monitorVariables)
    {
        app.Command("o", cmd =>
        {
            cmd.HelpOption(inherited: true);
            cmd.Description = "Show global options";
            cmd.AddName("options");

            cmd.Command("u", setRegisterCmd =>
                {
                    setRegisterCmd.Description = "Flag how to handle unknown instructions (0 = continue, 1 = stop).";
                    var uVal = setRegisterCmd.Argument("flag", "Unknown instruction flag").IsRequired();
                    uVal.Validators.Add(new MustBeIntegerFlag());

                    setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return monitor.WriteValidationError(validationResult);
                    });

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = uVal.Value;
                        monitor.Options.StopAfterUnknownInstruction = value == "1";
                        monitor.ApplyOptionsOnBreakPointExecEvaluator();
                        monitor.ShowOptions();
                        return (int)CommandResult.Ok;
                    });
                });

            cmd.Command("b", setRegisterCmd =>
            {
                setRegisterCmd.Description = "Flag how to handle BRK instruction (0 = continue, 1 = stop).";
                var bVal = setRegisterCmd.Argument("flag", "BRK instruction flag").IsRequired();
                bVal.Validators.Add(new MustBeIntegerFlag());

                setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return monitor.WriteValidationError(validationResult);
                });

                setRegisterCmd.OnExecute(() =>
                {
                    var value = bVal.Value;
                    monitor.Options.StopAfterBRKInstruction = value == "1";
                    monitor.ApplyOptionsOnBreakPointExecEvaluator();
                    monitor.ShowOptions();
                    return (int)CommandResult.Ok;
                });
            });

            cmd.OnValidationError((ValidationResult validationResult) =>
            {
                return monitor.WriteValidationError(validationResult);
            });

            cmd.OnExecute(() =>
            {
                monitor.ShowOptions();
                return (int)CommandResult.Ok;
            });
        });

        return app;
    }
}
