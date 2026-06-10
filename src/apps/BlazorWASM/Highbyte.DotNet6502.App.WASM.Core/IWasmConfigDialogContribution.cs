namespace Highbyte.DotNet6502.App.WASM;

public interface IWasmConfigDialogContribution
{
    Type ComponentType { get; }
    bool UseRenderProviderAndRenderTargetTypeCombinations { get; }
    bool UseAudioProviderAndAudioTargetTypeCombinations { get; }

    /// <summary>
    /// When true, the host passes the general browser <c>CorsProxyUrl</c> setting to the config UI
    /// (as a <c>CorsProxyUrl</c> parameter) so it can route cross-origin downloads through the proxy.
    /// </summary>
    bool UsesCorsProxy { get; }
}

public sealed record WasmConfigDialogContribution(
    Type ComponentType,
    bool UseRenderProviderAndRenderTargetTypeCombinations = false,
    bool UseAudioProviderAndAudioTargetTypeCombinations = false,
    bool UsesCorsProxy = false) : IWasmConfigDialogContribution;
