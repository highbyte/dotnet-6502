namespace Highbyte.DotNet6502.SadConsoleHost
{
    public class Options
    {
        public const string ConfigSectionName = "Highbyte.DotNet6502.SadConsoleHost";

        public SadConsoleConfig SadConsoleConfig { get; set; }

        public EmulatorConfig EmulatorConfig { get; set; }
    }
}