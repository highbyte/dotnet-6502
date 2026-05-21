using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Render;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Render.Legacy.v1;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Render.Legacy.v2;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Plugins;
using Highbyte.DotNet6502.Systems.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.SilkNet.Commodore64.C64SilkNetEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64;

/// <summary>
/// Engine-side plugin for the C64 on the SilkNet + NAudio host pair. Registers the C64
/// <see cref="ISystemConfigurer{TIn,TAu}"/> into DI and contributes the C64-specific render
/// targets to the SilkNet render pipeline (via <see cref="ISilkNetRenderTargetPlugin"/>).
/// </summary>
public sealed class C64SilkNetEnginePlugin
    : ISystemEnginePlugin, ISilkNetRenderTargetPlugin
{
    public string SystemName => C64.SystemName;

    public string HostTechName => "SilkNet.NAudio";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Engine-side configurer for the SilkNet + NAudio host pair.
        services.AddSingleton<ISystemConfigurer>(sp =>
            new C64Setup(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IConfiguration>()));
    }

    public void RegisterRenderTargets(RenderTargetProvider rtp, SilkNetRenderContext context)
    {
        // Legacy: simplified custom drawing with Skia commands. Supports characters and sprites, no bitmaps.
        rtp.AddRenderTargetType<C64LegacyRenderTarget>(() => new C64LegacyRenderTarget(
            GetC64(context),
            context.GetCanvas,
            flush: true));
        rtp.AddRenderTargetType<C64LegacyRenderTarget2>(() => new C64LegacyRenderTarget2(
            GetC64(context),
            context.GetCanvas,
            flush: true));

        // GPU based custom render target, specific to the C64 and the SilkNet OpenGL renderer.
        rtp.AddRenderTargetType<C64SilkNetOpenGlRendererTarget>(() => new C64SilkNetOpenGlRendererTarget(
            GetC64(context),
            ((C64HostConfig)context.GetCurrentHostSystemConfig()).SilkNetOpenGlRendererConfig,
            context.Gl,
            context.Window));
    }

    private static C64 GetC64(SilkNetRenderContext context)
        => context.GetCurrentRunningSystem() as C64
           ?? throw new DotNet6502Exception("Current running system is not a C64.");
}
