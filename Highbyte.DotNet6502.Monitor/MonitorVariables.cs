namespace Highbyte.DotNet6502.Monitor
{
    /// <summary>
    /// Working variables for monitor
    /// </summary>
    public class MonitorVariables
    {
        public ushort? LatestDisassemblyAddress { get; set; }
        public ushort? LatestMemoryDumpAddress { get; set; }
    }
}