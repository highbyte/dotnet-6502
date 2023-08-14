namespace Highbyte.DotNet6502.Monitor.SystemSpecific;

/// <summary>
/// </summary>
public interface ISystemMonitor
{
    public ISystemMonitorCommands GetSystemMonitorCommands();
}