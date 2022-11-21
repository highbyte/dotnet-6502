using System.ComponentModel.DataAnnotations;
using Highbyte.DotNet6502.Monitor.Commands;
using Highbyte.DotNet6502.Monitor.SystemSpecific;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor;

/// <summary>
/// </summary>
public class CommandLineApp
{
    public static CommandLineApplication Build(MonitorBase monitor, MonitorVariables monitorVariables, MonitorConfig options)
    {
        //var app = new CommandLineApplication()
        //var app = new CommandLineApplication(NullConsole.Singleton, monitor.Options.DefaultDirectory)
        var app = new CommandLineApplication(MonitorConsole.BuildSingleton(monitor), monitor.Options.DefaultDirectory)
        {
            Name = "",
            Description = "DotNet 6502 machine code monitor for the DotNet 6502 emulator library." + Environment.NewLine +
                          "By Highbyte 2022" + Environment.NewLine +
                          "Source at: https://github.com/highbyte/dotnet-6502",
            UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.StopParsingAndCollect
        };

        // Fix: Use custom Help Text Generator to avoid name/description of the application to be shown each time help text is shown.
        app.HelpTextGenerator = new CustomHelpTextGenerator(options.MaxLineLength);
        // Fix: To avoid CommandLineUtils to the name of the application at the end of the help text: Don't use HelpOption on app-level, instead set it on each command below.
        //app.HelpOption(inherited: true);

        app.ConfigureRegisters(monitor, monitorVariables);
        app.ConfigureMemory(monitor, monitorVariables);
        app.ConfigureDisassembly(monitor, monitorVariables);
        app.ConfigureExecution(monitor, monitorVariables);
        app.ConfigureBreakpoints(monitor, monitorVariables);
        app.ConfigureFiles(monitor, monitorVariables);
        app.ConfigureReset(monitor, monitorVariables);

        // Add any system-specific monitor commands if the system implements it.
        if (monitor.SystemRunner.System is ISystemMonitor systemWithMonitor)
        {
            var monitorCommands = systemWithMonitor.GetSystemMonitorCommands();
            monitorCommands.Configure(app, monitor);
        }

        app.Command("q", cmd =>
        {
            cmd.HelpOption(inherited: true);
            cmd.Description = "Quit monitor.";
            cmd.AddName("quit");
            cmd.AddName("x");
            cmd.AddName("exit");

            cmd.OnValidationError((ValidationResult validationResult) =>
            {
                return monitor.WriteValidationError(validationResult);
            });

            cmd.OnExecute(() =>
            {
                return (int)CommandResult.Quit;
            });
        });

        app.OnExecute(() =>
        {
            monitor.WriteOutput("Unknown command.", MessageSeverity.Error);
            monitor.WriteOutput("Help: ?|help|-?|--help", MessageSeverity.Information);
            monitor.WriteOutput("Help: command -?|-h|--help", MessageSeverity.Information);
            return (int)CommandResult.Error;
        });

        return app;
    }
}
