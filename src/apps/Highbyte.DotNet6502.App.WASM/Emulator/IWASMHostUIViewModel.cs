namespace Highbyte.DotNet6502.App.WASM.Emulator;

public interface IWASMHostUIViewModel
{
    Task SetDebugState(bool visible);
    Task ToggleDebugState();
    void UpdateDebug(string debug);

    void UpdateStats(string stats);
    Task SetStatsState(bool visible);
    Task ToggleStatsState();

    Task SetMonitorState(bool visible);
}
