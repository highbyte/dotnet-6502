using Highbyte.DotNet6502.Systems;
using Silk.NET.OpenGL;
using SkiaSharp;

namespace Highbyte.DotNet6502.Impl.SilkNet;

/// <summary>
/// Host runtime handles passed to <see cref="ISilkNetRenderTargetPlugin"/> implementations so
/// they can build render targets without depending on the SilkNet host app.
/// </summary>
/// <remarks>
/// <see cref="Gl"/> and <see cref="Window"/> exist by the time the host configures rendering.
/// The system / host-config getters are lazy because render-target factories run later — once a
/// system has been selected and started.
/// </remarks>
public sealed class SilkNetRenderContext
{
    public GL Gl { get; }
    public IWindow Window { get; }
    public Func<SKCanvas> GetCanvas { get; }
    public Func<ISystem> GetCurrentRunningSystem { get; }
    public Func<IHostSystemConfig> GetCurrentHostSystemConfig { get; }

    public SilkNetRenderContext(
        GL gl,
        IWindow window,
        Func<SKCanvas> getCanvas,
        Func<ISystem> getCurrentRunningSystem,
        Func<IHostSystemConfig> getCurrentHostSystemConfig)
    {
        Gl = gl;
        Window = window;
        GetCanvas = getCanvas;
        GetCurrentRunningSystem = getCurrentRunningSystem;
        GetCurrentHostSystemConfig = getCurrentHostSystemConfig;
    }
}
