using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Highbyte.DotNet6502.Monitor.Commands;
using Highbyte.DotNet6502.Monitor.SystemSpecific;

namespace Highbyte.DotNet6502.Monitor;

/// <summary>
/// </summary>
public static class CommandLineApp
{
    public static Parser Build(MonitorBase monitor, MonitorVariables monitorVariables, MonitorConfig options, IConsole console)
    {

        Parser? parser = null;
        var root = new Command(
            "DotNet6502Monitor",
            "DotNet 6502 machine code monitor for the DotNet 6502 emulator library." + Environment.NewLine +
            "By Highbyte 2024." + Environment.NewLine +
            "Source at: https://github.com/highbyte/dotnet-6502")
        {
        };

        root.ConfigureRegisters(monitor, monitorVariables);
        root.ConfigureMemory(monitor, monitorVariables);
        root.ConfigureDisassembly(monitor, monitorVariables);
        root.ConfigureExecution(monitor, monitorVariables);
        root.ConfigureBreakpoints(monitor, monitorVariables);
        root.ConfigureFiles(monitor, monitorVariables);
        root.ConfigureReset(monitor, monitorVariables);
        root.ConfigureOptions(monitor, monitorVariables);

        // Add any system-specific monitor commands if the system implements it.
        if (monitor.SystemRunner.System is ISystemMonitor systemWithMonitor)
        {
            var monitorCommands = systemWithMonitor.GetSystemMonitorCommands();
            monitorCommands.Configure(root, monitor);
        }

        var quitCmd = new Command("q", "Quit monitor");
        quitCmd.AddAlias("quit");
        quitCmd.AddAlias("exit");
        quitCmd.SetHandler(() => Task.FromResult((int)CommandResult.Quit));
        root.AddCommand(quitCmd);

        var helpCmd = new Command("?", "Help");
        helpCmd.SetHandler(() =>
        {
            parser?.Invoke($"{root.Name} -?", console);
        });
        root.AddCommand(helpCmd);

        int maxWidth = options.MaxLineLength ?? int.MaxValue;
        var cmdLineBuilder = new CommandLineBuilder(root)
            .UseHelpBuilder(_ =>
            {
                return new CustomHelpBuilderWithourRootCommand(LocalizationResources.Instance, root.Name, maxWidth: maxWidth);
            })
            .UseHelp();

        parser = cmdLineBuilder.Build();
        return parser;
    }
}
