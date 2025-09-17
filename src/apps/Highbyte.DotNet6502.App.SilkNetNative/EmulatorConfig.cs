using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SilkNetNative;

public class EmulatorConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.SilkNetNativeConfig";

    public string DefaultEmulator { get; set; }
    public float DefaultDrawScale { get; set; }
    public float CurrentDrawScale { get; set; }
    public MonitorConfig Monitor { get; set; }

    public EmulatorConfig()
    {
        DefaultDrawScale = 3.0f;
        Monitor = new MonitorConfig
        {
            MaxLineLength = 100
        };
    }

    public void Validate(SystemList<SilkNetInputHandlerContext, NAudioAudioHandlerContext> systemList)
    {
        if (!systemList.Systems.Contains(DefaultEmulator))
            throw new DotNet6502Exception($"Setting {nameof(DefaultEmulator)} value {DefaultEmulator} is not supported. Valid values are: {string.Join(',', systemList.Systems)}");
        Monitor.Validate();
    }
}
