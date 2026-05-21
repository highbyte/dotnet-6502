using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.Impl.AspNet;

/// <summary>
/// Optional capability interface an engine plug-in (<c>ISystemEnginePlugin</c>) implements when
/// it contributes system-specific render targets to the WASM/Skia host's render pipeline.
/// </summary>
/// <remarks>
/// The host registers only system-agnostic render targets itself, then invokes every discovered
/// implementation so plug-ins can add their own. This keeps system-specific render code out of
/// <c>SkiaWASMHostApp</c>. Mirrors the SilkNet-side <c>ISilkNetRenderTargetPlugin</c> pattern.
/// </remarks>
public interface ISkiaWasmRenderTargetPlugin
{
    /// <summary>
    /// Add this plug-in's render targets to <paramref name="rtp"/>. Called once from the host's
    /// render-config callback. Use <paramref name="context"/> for the Skia canvas and a lazy
    /// getter for the current running system.
    /// </summary>
    void RegisterRenderTargets(RenderTargetProvider rtp, SkiaWasmRenderContext context);
}
