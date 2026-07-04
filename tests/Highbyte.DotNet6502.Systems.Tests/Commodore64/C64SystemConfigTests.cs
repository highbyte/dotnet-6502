using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

public class C64SystemConfigTests
{
    [Fact]
    public void ROMDirectory_WhenBlank_Uses_DefaultROMDirectory()
    {
        var config = new C64SystemConfig();

        Assert.Equal(string.Empty, config.ROMDirectory);
        Assert.Equal(C64SystemConfig.DefaultROMDirectory, config.EffectiveROMDirectory);
    }

    [Fact]
    public void ROMDirectory_WhenSet_Uses_Override()
    {
        var config = new C64SystemConfig
        {
            ROMDirectory = "/custom/c64"
        };

        Assert.Equal("/custom/c64", config.EffectiveROMDirectory);
    }

    [Fact]
    public void IsValid_Includes_SwiftLink_Validation_Errors()
    {
        var config = new C64SystemConfig();
        config.SwiftLink.InterruptMode = (C64SwiftLinkInterruptMode)999;

        var isValid = config.IsValid(out var validationErrors);

        Assert.False(isValid);
        Assert.Contains(
            $"{nameof(C64SystemConfig.SwiftLink)}.{nameof(C64SwiftLinkConfig.InterruptMode)} has an invalid value.",
            validationErrors);
    }
}
