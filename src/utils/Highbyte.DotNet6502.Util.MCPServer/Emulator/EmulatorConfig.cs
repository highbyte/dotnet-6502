using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Util.MCPServer.Emulator.SystemSetup;

namespace Highbyte.DotNet6502.Util.MCPServer.Emulator;

public class EmulatorConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.MCPServer";

    /// <summary>
    /// The name of the emulator to start.
    /// Ex: GenericComputer, C64
    /// </summary>
    /// <value></value>
    public string DefaultEmulator { get; set; }

    public MonitorConfig Monitor { get; set; }


    /// <summary>
    /// SadConsole-specific configuration for specific system.
    /// </summary>
    public C64HostConfig C64HostConfig { get; set; }

    public EmulatorConfig()
    {
        Monitor = new();
        C64HostConfig = new();
    }

    public void Validate(SystemList<NullRenderContext, NullInputHandlerContext, NullAudioHandlerContext> systemList)
    {
        if (!systemList.Systems.Contains(DefaultEmulator))
            throw new DotNet6502Exception($"Setting {nameof(DefaultEmulator)} value {DefaultEmulator} is not supported. Valid values are: {string.Join(',', systemList.Systems)}");
        //Monitor.Validate();
    }
}
