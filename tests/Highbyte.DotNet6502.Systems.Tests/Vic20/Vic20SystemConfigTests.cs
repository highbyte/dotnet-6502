using Highbyte.DotNet6502.Systems.Vic20.Config;

namespace Highbyte.DotNet6502.Systems.Tests.Vic20;

public class Vic20SystemConfigTests
{
    [Fact]
    public void ROMDirectory_WhenBlank_Uses_DefaultROMDirectory()
    {
        var config = new Vic20SystemConfig();

        Assert.Equal(string.Empty, config.ROMDirectory);
        Assert.Equal(Vic20SystemConfig.DefaultROMDirectory, config.EffectiveROMDirectory);
    }

    [Fact]
    public void ROMDirectory_WhenSet_Uses_Override()
    {
        var config = new Vic20SystemConfig
        {
            ROMDirectory = "/custom/vic20"
        };

        Assert.Equal("/custom/vic20", config.EffectiveROMDirectory);
    }
}
