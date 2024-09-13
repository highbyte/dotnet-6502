namespace Highbyte.DotNet6502.App.SadConsole.SystemSetup;

public class C64HostConfig : SadConsoleHostSystemConfigBase
{
    public bool BasicAIAssistantEnabled { get; set; }
    public bool BasicAIAssistantDefaultEnabled { get; set; }
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
