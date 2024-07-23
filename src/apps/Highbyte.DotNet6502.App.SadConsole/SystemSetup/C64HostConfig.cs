using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SadConsole.SystemSetup;

public class C64HostConfig : SadConsoleHostSystemConfigBase
{
    public C64HostConfig()
    {
        Font = "Fonts/C64.font";
        FontScale = 1;
    }

    public new object Clone()
    {
        var clone = (C64HostConfig)MemberwiseClone();
        return clone;
    }
}
