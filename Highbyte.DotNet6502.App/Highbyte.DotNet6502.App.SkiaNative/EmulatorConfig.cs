using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

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

    public void Validate(SystemList<SkiaRenderContext, SilkNetInputHandlerContext, NullSoundHandlerContext> systemList)
    {
        if (!systemList.Systems.Contains(DefaultEmulator))
            throw new Exception($"Setting {nameof(DefaultEmulator)} value {DefaultEmulator} is not supported. Valid values are: {string.Join(',', systemList.Systems)}");
        Monitor.Validate();
    }
}
