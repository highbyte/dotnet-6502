using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Render.Legacy.v1;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Render.Legacy.v2;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Plugins;
using Highbyte.DotNet6502.Systems.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.AspNet.Commodore64.C64AspNetEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64;

/// <summary>
/// Engine-side plugin for the C64 on the WASM (Blazor) + WebAudio host pair. Registers the C64
/// <see cref="ISystemConfigurer{TIn,TAu}"/> (<see cref="C64Setup"/>) into DI and contributes the
/// C64-specific Skia render targets to the WASM render pipeline (via
/// <see cref="ISkiaWasmRenderTargetPlugin"/>).
/// </summary>
public sealed class C64AspNetEnginePlugin
    : ISystemEnginePlugin, ISkiaWasmRenderTargetPlugin
{
    public string SystemName => C64.SystemName;

    public string HostTechName => "WASM.WebAudio";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Engine-side configurer for the WASM + WebAudio host pair. Scoped — matches the Blazor
        // WASM service lifetime used for the rest of the per-system services.
        services.AddScoped<ISystemConfigurer>(sp =>
            new C64Setup(
                sp.GetRequiredService<BrowserContext>(),
                sp.GetRequiredService<ILoggerFactory>()));
    }

    public void RegisterRenderTargets(RenderTargetProvider rtp, SkiaWasmRenderContext context)
    {
        // Legacy: simplified custom drawing with Skia commands. Supports characters and sprites, no bitmaps.
        rtp.AddRenderTargetType<C64LegacyRenderTarget>(() => new C64LegacyRenderTarget(
            GetC64(context),
            context.GetCanvas,
            flush: false));
        rtp.AddRenderTargetType<C64LegacyRenderTarget2>(() => new C64LegacyRenderTarget2(
            GetC64(context),
            context.GetCanvas,
            flush: false));
    }

    private static C64 GetC64(SkiaWasmRenderContext context)
        => context.GetCurrentRunningSystem() as C64
           ?? throw new DotNet6502Exception("Current running system is not a C64.");
}
