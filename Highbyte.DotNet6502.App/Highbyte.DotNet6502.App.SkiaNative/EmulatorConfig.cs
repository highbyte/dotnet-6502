using Highbyte.DotNet6502.Monitor;

namespace Highbyte.DotNet6502.App.SkiaNative;

public class EmulatorConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.SkiaConfig";

    public string Emulator { get; set; }
    public float DrawScale { get; set; }
    public MonitorConfig Monitor { get; set; }

    public EmulatorConfig()
    {
        DrawScale = 3.0f;
    }

    public void Validate()
    {
        if (!SystemList.SystemNames.Contains(Emulator))
            throw new Exception($"Setting {nameof(Emulator)} value {Emulator} is not supported. Valid values are: {string.Join(',', SystemList.SystemNames)}");
        Monitor.Validate();
    }
}
