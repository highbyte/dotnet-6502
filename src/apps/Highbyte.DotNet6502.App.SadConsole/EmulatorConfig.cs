using Highbyte.DotNet6502.App.SadConsole.SystemSetup;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using static SadConsole.IFont;

namespace Highbyte.DotNet6502.App.SadConsole;

public class EmulatorConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.SadConsoleConfig";

    public string WindowTitle { get; set; }

    /// <summary>
    /// Optional. Font used for the UI. If not specified, default SadConsole font is used.
    /// To use a specific  SadConsole Font, include it in your program output directory.
    /// </summary>
    public string? UIFont { get; set; }

    /// <summary>
    /// Note: UI FontSize other than One is not currently implemented.
    /// Font size for the UI.
    /// Sizes.One is default.
    /// </summary>
    /// <value></value>
    public Sizes UIFontSize { get; set; }

    /// <summary>
    /// The name of the emulator to start.
    /// Ex: GenericComputer, C64
    /// </summary>
    /// <value></value>
    public string DefaultEmulator { get; set; }


    public MonitorConfig Monitor { get; set; }

    /// <summary>
    /// Initial audio volume in percent.
    /// </summary>
    public int DefaultAudioVolumePercent { get; set; }

    /// <summary>
    /// SadConsole-specific configuration for specific system.
    /// </summary>
    public C64HostConfig C64HostConfig { get; set; }
    /// <summary>
    /// SadConsole-specific configuration for specific system.
    /// </summary>
    public GenericComputerHostConfig GenericComputerHostConfig { get; set; }

    public EmulatorConfig()
    {
        WindowTitle = "SadConsole + Highbyte.DotNet6502 emulator.";
        UIFont = null;
        UIFontSize = Sizes.One;
        DefaultEmulator = "C64";

        Monitor = new();

        DefaultAudioVolumePercent = 20;

        C64HostConfig = new();
        GenericComputerHostConfig = new();
    }

    public void Validate(SystemList<SadConsoleRenderContext, SadConsoleInputHandlerContext, NAudioAudioHandlerContext> systemList)
    {
        if (!systemList.Systems.Contains(DefaultEmulator))
            throw new DotNet6502Exception($"Setting {nameof(DefaultEmulator)} value {DefaultEmulator} is not supported. Valid values are: {string.Join(',', systemList.Systems)}");
        //Monitor.Validate();
    }
}
