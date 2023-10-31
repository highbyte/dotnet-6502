using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.WASM.Skia;

public class EmulatorConfig
{
    public const int DEFAULT_CANVAS_WINDOW_WIDTH = 640;
    public const int DEFAULT_CANVAS_WINDOW_HEIGHT = 400;

    public string DefaultEmulator { get; set; }
    public double DefaultDrawScale { get; set; }
    public double CurrentDrawScale { get; set; }
    public MonitorConfig? Monitor { get; set; }

    public Dictionary<string, IHostSystemConfig> HostSystemConfigs = new();

    public EmulatorConfig()
    {
        DefaultDrawScale = 2.0;
        CurrentDrawScale = DefaultDrawScale;
    }

    public void Validate(SystemList<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext> systemList)
    {
        if (!systemList.Systems.Contains(DefaultEmulator))
            throw new Exception($"Setting {nameof(DefaultEmulator)} value {DefaultEmulator} is not supported. Valid values are: {string.Join(',', systemList.Systems)}");
        Monitor.Validate();
    }
}
