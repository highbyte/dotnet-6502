namespace Highbyte.DotNet6502.App.WASM;

public interface IWasmConfigDialogContribution
{
    Type ComponentType { get; }
    bool UseRenderProviderAndRenderTargetTypeCombinations { get; }
}

public sealed record WasmConfigDialogContribution(
    Type ComponentType,
    bool UseRenderProviderAndRenderTargetTypeCombinations = false) : IWasmConfigDialogContribution;
