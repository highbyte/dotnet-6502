using Highbyte.DotNet6502.Monitor;

namespace Highbyte.DotNet6502.App.SkiaNative;

public class EmulatorConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.SkiaConfig";

    public string DefaultEmulator { get; set; }
    public float DefaultDrawScale { get; set; }
    public MonitorConfig Monitor { get; set; }

    public EmulatorConfig()
    {
        DefaultDrawScale = 3.0f;
    }

    public void Validate()
    {
        if (!SystemList.SystemNames.Contains(DefaultEmulator))
            throw new Exception($"Setting {nameof(DefaultEmulator)} value {DefaultEmulator} is not supported. Valid values are: {string.Join(',', SystemList.SystemNames)}");
        Monitor.Validate();
    }
}
