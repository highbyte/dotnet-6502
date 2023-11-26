using System.CommandLine;

namespace Highbyte.DotNet6502.Monitor.SystemSpecific;

/// <summary>
/// </summary>
public interface ISystemMonitorCommands
{
    public void Configure(Command rootCommand, MonitorBase monitor);
    public void Reset(MonitorBase monitor);
}
