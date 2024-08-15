namespace Highbyte.DotNet6502.App.SadConsole.SystemSetup;

public class C64HostConfig : SadConsoleHostSystemConfigBase
{
    public C64HostConfig()
    {
        //Font = "Fonts/C64.font";
        //DefaultFontSize = IFont.Sizes.One;

        Font = "Fonts/C64_ROM.font";
        DefaultFontSize = IFont.Sizes.Two;
    }

    public new object Clone()
    {
        var clone = (C64HostConfig)MemberwiseClone();
        return clone;
    }
}
