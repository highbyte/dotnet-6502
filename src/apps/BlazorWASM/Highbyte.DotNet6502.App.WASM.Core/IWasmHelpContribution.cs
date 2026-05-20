namespace Highbyte.DotNet6502.App.WASM;

public interface IWasmHelpContribution
{
    Type ComponentType { get; }
}

public sealed record WasmHelpContribution(Type ComponentType) : IWasmHelpContribution;
