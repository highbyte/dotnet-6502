using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Systems.Generic.Config;
using SadConsole;

namespace Highbyte.DotNet6502.Impl.SadConsole.Generic;

/// <summary>Generic-computer host config for the SadConsole host.</summary>
public class GenericComputerHostConfig : SadConsoleHostSystemConfigBase<GenericComputerSystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.GenericComputer.SadConsole";

    public override bool AudioSupported => false;

    public GenericComputerHostConfig()
    {
        DefaultFontSize = IFont.Sizes.One;
    }
}
