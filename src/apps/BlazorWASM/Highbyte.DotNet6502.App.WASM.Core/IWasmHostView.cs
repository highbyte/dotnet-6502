using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Highbyte.DotNet6502.App.WASM;

public interface IWasmHostView
{
    bool Initialized { get; }
    Emulator.Skia.SkiaWASMHostApp WasmHost { get; }
    string SelectedSystemName { get; }
    string SelectedSystemConfigurationVariant { get; }
    EmulatorState CurrentEmulatorState { get; }
    ISystem? CurrentRunningSystem { get; }
    SystemRunner? CurrentSystemRunner { get; }
    IHostSystemConfig CurrentHostSystemConfig { get; }

    Task OnStart(MouseEventArgs mouseEventArgs);
    Task OnPause(MouseEventArgs mouseEventArgs);
    Task OnReset(MouseEventArgs mouseEventArgs);
    Task OnStop(MouseEventArgs mouseEventArgs);

    Task StartAsync();
    Task PauseAsync();
    Task ResetAsync();
    Task StopAsync();
    Task FocusEmulator();
    Task UpdateCanvasSize();
    Task ShowCurrentConfigUI();
    Task ShowHelpUI(Type componentType);
    Task SelectSystemConfigurationVariant(string systemConfigurationVariant);
    Task PersistCurrentHostSystemConfig();
    void UpdateHostSystemConfig(IHostSystemConfig hostSystemConfig);

    IEnumerable<Type> GetAvailableSystemRenderProviderTypes();
    IEnumerable<(Type renderProviderType, Type renderTargetType)> GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations();
    IEnumerable<(Type audioProviderType, Type audioTargetType)> GetAvailableSystemAudioProviderTypesAndAudioTargetTypeCombinations();

    string GetSystemVisibilityDisplayStyle(string displayData, string systemName);

    Task SetDebugState(bool visible);
    Task ToggleDebugState();
    void UpdateDebug(string debug);
    Task SetStatsState(bool visible);
    Task ToggleStatsState();
    void UpdateStats(string stats);
    Task SetMonitorState(bool visible);
}
