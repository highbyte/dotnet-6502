using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor.Commands
{
    /// <summary>
    /// </summary>
    public static class ResetCommands
    {
        public static CommandLineApplication ConfigureReset(this CommandLineApplication app, MonitorBase monitor, MonitorVariables monitorVariables)
        {
            app.Command("reset", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Resets the computer (soft, memory intact).";

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return monitor.WriteValidationError(validationResult);
                });

                cmd.OnExecute(() =>
                {
                    monitor.Cpu.Reset(monitor.Mem); // A soft reset, memory not cleared.
                    return (int)CommandResult.Continue;
                });
            });

            return app;
        }
    }
}