using Highbyte.DotNet6502.Monitor;

namespace Highbyte.DotNet6502.App.SkiaNative
{
    public class EmulatorConfig
    {
        public const string ConfigSectionName = "Highbyte.DotNet6502.SkiaConfig";

        public string Emulator { get; set; }
        public MonitorConfig Monitor { get; set; }

        public void Validate()
        {
            if (!SystemList.SystemsNames.Contains(Emulator))
                throw new Exception($"Setting {nameof(Emulator)} value {Emulator} is not supported. Valid values are: {string.Join(',', SystemList.SystemsNames)}");

            Monitor.Validate();
        }
    }
}