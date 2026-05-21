using System;
using Highbyte.DotNet6502.Systems;
using SkiaSharp;

namespace Highbyte.DotNet6502.Impl.AspNet;

/// <summary>
/// Host runtime handles passed to <see cref="ISkiaWasmRenderTargetPlugin"/> implementations so
/// they can build render targets without depending on the WASM host app.
/// </summary>
/// <remarks>
/// The getters are lazy because render-target factories run later — once a system has been
/// selected and started.
/// </remarks>
public sealed class SkiaWasmRenderContext
{
    public Func<SKCanvas> GetCanvas { get; }
    public Func<ISystem> GetCurrentRunningSystem { get; }

    public SkiaWasmRenderContext(
        Func<SKCanvas> getCanvas,
        Func<ISystem> getCurrentRunningSystem)
    {
        GetCanvas = getCanvas;
        GetCurrentRunningSystem = getCurrentRunningSystem;
    }
}
