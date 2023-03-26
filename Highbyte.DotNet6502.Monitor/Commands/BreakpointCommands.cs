using System.ComponentModel.DataAnnotations;
using System.Globalization;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class BreakpointCommands
{
    public static CommandLineApplication ConfigureBreakpoints(this CommandLineApplication app, MonitorBase monitor, MonitorVariables monitorVariables)
    {
        app.Command("b", cmd =>
        {
            cmd.HelpOption(inherited: true);
            cmd.Description = "Breakpoints";
            cmd.AddName("bp");
            cmd.AddName("breakpoint");

            cmd.Command("l", bpCmd =>
                {
                    bpCmd.Description = "Lists all breakpoints.";
                    bpCmd.OnExecute(() =>
                    {
                        return ListBreakpoints(monitor);
                    });
                });

            cmd.Command("a", bpCmd =>
                {
                    bpCmd.Description = "Add a breakpoint.";
                    var memAddress = bpCmd.Argument("address", "Memory address 16 bits (hex).").IsRequired();
                    memAddress.Validators.Add(new MustBe16BitHexValueValidator());

                    bpCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return monitor.WriteValidationError(validationResult);
                    });

                    bpCmd.OnExecute(() =>
                    {
                        var address = ushort.Parse(memAddress.Value, NumberStyles.AllowHexSpecifier, null);
                        if (!monitor.BreakPoints.ContainsKey(address))
                            monitor.BreakPoints.Add(address, new BreakPoint { Enabled = true });
                        else
                            monitor.BreakPoints[address].Enabled = true;
                        return (int)CommandResult.Ok;
                    });
                });

            cmd.Command("d", bpCmd =>
                {
                    bpCmd.Description = "Delete a breakpoint.";
                    var memAddress = bpCmd.Argument("address", "Memory address 16 bits (hex).").IsRequired();
                    memAddress.Validators.Add(new MustBe16BitHexValueValidator());

                    bpCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return monitor.WriteValidationError(validationResult);
                    });

                    bpCmd.OnExecute(() =>
                    {
                        var address = ushort.Parse(memAddress.Value, NumberStyles.AllowHexSpecifier, null);
                        if (monitor.BreakPoints.ContainsKey(address))
                            monitor.BreakPoints.Remove(address);
                        return (int)CommandResult.Ok;
                    });
                });

            cmd.Command("da", bpCmd =>
                {
                    bpCmd.Description = "Delete all breakpoints.";
                    bpCmd.OnExecute(() =>
                    {
                        monitor.BreakPoints.Clear();
                        return (int)CommandResult.Ok;
                    });
                });

            cmd.OnExecute(() =>
            {
                return ListBreakpoints(monitor);
            });
        });

        return app;
    }

    private static int ListBreakpoints(MonitorBase monitor)
    {
        if (monitor.BreakPoints.Count == 0)
            monitor.WriteOutput($"No breakpoints.");
        else
            monitor.WriteOutput($"Breakpoints:");

        foreach (var bp in monitor.BreakPoints.Keys)
        {
            var addr = bp.ToHex();
            var status = monitor.BreakPoints[bp].Enabled ? "Enabled" : "Disabled";
            monitor.WriteOutput($"{addr} : {status}");
        }
        return (int)CommandResult.Ok;
    }
}