using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Systems.Vic20;
using Highbyte.DotNet6502.Systems.Vic20.Render;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

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

    [Fact]
    public async Task GetNewHostSystemConfig_Applies_Render_Type_Overrides_From_Configuration()
    {
        var renderProviderType = typeof(Vic20VideoCommandStream);
        var renderTargetType = typeof(TestRenderTarget);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Vic20:SystemConfig:RenderProviderType"] = renderProviderType.AssemblyQualifiedName,
                ["Vic20:SystemConfig:RenderTargetType"] = renderTargetType.AssemblyQualifiedName
            })
            .Build();

        var configurer = new Vic20SystemConfigurerCore(
            NullLoggerFactory.Instance,
            configuration,
            () => new TestVic20HostConfig(),
            "Vic20");

        var hostConfig = (TestVic20HostConfig)await configurer.GetNewHostSystemConfig();

        Assert.Equal(renderProviderType, hostConfig.SystemConfig.RenderProviderType);
        Assert.Equal(renderTargetType, hostConfig.SystemConfig.RenderTargetType);
    }

    private sealed class TestVic20HostConfig : HostSystemConfigBase<Vic20SystemConfig>
    {
        public override bool AudioSupported => false;
    }

    private sealed class TestRenderTarget
    {
    }
}
