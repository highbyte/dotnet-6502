namespace Highbyte.DotNet6502.Monitor.SystemSpecific
{
    /// <summary>
    /// </summary>
    public interface IMonitor
    {
        public IMonitorCommands GetMonitorCommands();
    }
}