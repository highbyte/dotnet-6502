namespace Highbyte.DotNet6502.App.WASM;

public interface IWasmMenuContribution
{
    Type ComponentType { get; }
}

public sealed record WasmMenuContribution(Type ComponentType) : IWasmMenuContribution;
