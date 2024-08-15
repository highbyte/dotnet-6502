namespace Highbyte.DotNet6502.App.SadConsole.SystemSetup;

public class GenericComputerHostConfig : SadConsoleHostSystemConfigBase
{
    public GenericComputerHostConfig()
    {
        Font = null;
        DefaultFontSize = IFont.Sizes.One;
    }

    public new object Clone()
    {
        var clone = (GenericComputerHostConfig)MemberwiseClone();
        return clone;
    }
}
