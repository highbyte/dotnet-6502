using Highbyte.DotNet6502.App.Avalonia.Core.Input;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

public class EmulatorConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.AvaloniaConfig";

    public string DefaultEmulator { get; set; } = "C64";
    public float DefaultDrawScale { get; set; } = 2.0f;
    public float CurrentDrawScale { get; set; } = 2.0f;
    public bool ShowErrorDialog { get; set; } = true;
    public MonitorConfig Monitor { get; set; } = new();

    public EmulatorConfig()
    {
        DefaultEmulator = DefaultEmulator;
        DefaultDrawScale = DefaultDrawScale;
        CurrentDrawScale = DefaultDrawScale;
        ShowErrorDialog = true;

        // Initialize MonitorConfig or other properties as needed
        Monitor = new();
    }

    public void Validate(SystemList<AvaloniaInputHandlerContext, NullAudioHandlerContext> systemList)
    {
        if (!systemList.Systems.Contains(DefaultEmulator))
            throw new DotNet6502Exception($"Setting {nameof(DefaultEmulator)} value {DefaultEmulator} is not supported. Valid values are: {string.Join(',', systemList.Systems)}");
        Monitor.Validate();
    }
}
