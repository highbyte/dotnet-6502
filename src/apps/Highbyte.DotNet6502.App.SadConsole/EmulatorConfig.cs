using Highbyte.DotNet6502.App.SadConsole.SystemSetup;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SadConsole;

public class EmulatorConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.SadConsoleConfig";

    public string WindowTitle { get; set; }

    /// <summary>
    /// Optional. If not specified, default SadConsole font is used.
    /// To use a specific  SadConsole Font, include it in your program output directory.
    /// </summary>
    public string? UIFont { get; set; }

    /// <summary>
    /// The name of the emulator to start.
    /// Ex: GenericComputer, C64
    /// </summary>
    /// <value></value>
    public string DefaultEmulator { get; set; }


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
        DefaultEmulator = "C64";

        C64HostConfig = new();
        GenericComputerHostConfig = new();
    }

    public void Validate(SystemList<SadConsoleRenderContext, SadConsoleInputHandlerContext, NullAudioHandlerContext> systemList)
    {
        if (!systemList.Systems.Contains(DefaultEmulator))
            throw new DotNet6502Exception($"Setting {nameof(DefaultEmulator)} value {DefaultEmulator} is not supported. Valid values are: {string.Join(',', systemList.Systems)}");
        //Monitor.Validate();
    }
}
