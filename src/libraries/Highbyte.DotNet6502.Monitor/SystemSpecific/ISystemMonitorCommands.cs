using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor.SystemSpecific;

/// <summary>
/// </summary>
public interface ISystemMonitorCommands
{
    public void Configure(CommandLineApplication app, MonitorBase monitor);
    public void Reset(MonitorBase monitor);
}