using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.Impl.SilkNet;

/// <summary>
/// Optional capability interface an engine plug-in (<c>ISystemEnginePlugin</c>) implements when
/// it contributes system-specific render targets to the SilkNet host's render pipeline.
/// </summary>
/// <remarks>
/// The host registers only system-agnostic render targets itself, then invokes every discovered
/// implementation so plug-ins can add their own. This keeps system-specific render code out of
/// <c>SilkNetHostApp</c>. Mirrors the shell-side <c>IAvaloniaNativeMenuPlugin</c> capability pattern.
/// </remarks>
public interface ISilkNetRenderTargetPlugin
{
    /// <summary>
    /// Add this plug-in's render targets to <paramref name="rtp"/>. Called once from the host's
    /// render-config callback. Use <paramref name="context"/> for host runtime handles (GL,
    /// window, Skia canvas) and lazy getters for the current system / host config.
    /// </summary>
    void RegisterRenderTargets(RenderTargetProvider rtp, SilkNetRenderContext context);
}
