using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.WASM.Emulator;

public class EmulatorConfig
{
    public const int DEFAULT_CANVAS_WINDOW_WIDTH = 640;
    public const int DEFAULT_CANVAS_WINDOW_HEIGHT = 400;

    public required string DefaultEmulator { get; set; }
    public double DefaultDrawScale { get; set; }
    public double CurrentDrawScale { get; set; }
    public required MonitorConfig Monitor { get; set; }

    public EmulatorConfig()
    {
        DefaultDrawScale = 2.0;
        CurrentDrawScale = DefaultDrawScale;
    }

    public void Validate(SystemList<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext> systemList)
    {
        if (!systemList.Systems.Contains(DefaultEmulator))
            throw new DotNet6502Exception($"Setting {nameof(DefaultEmulator)} value {DefaultEmulator} is not supported. Valid values are: {string.Join(',', systemList.Systems)}");
        Monitor.Validate();
    }
}
