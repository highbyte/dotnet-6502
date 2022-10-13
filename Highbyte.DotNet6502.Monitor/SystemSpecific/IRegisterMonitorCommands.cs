using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor.SystemSpecific
{
    /// <summary>
    /// </summary>
    public interface IRegisterMonitorCommands
    {
        public void Configure(CommandLineApplication app, MonitorBase monitor);
    }
}