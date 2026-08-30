using Highbyte.DotNet6502.Impl.Headless.Oric;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Oric;
using Highbyte.DotNet6502.Systems.Oric.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricHeadlessIntegrationTests
{
    [Fact]
    public async Task Headless_Configurer_Binds_Oric_Settings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OricHeadlessHostConfig.ConfigSectionName}:SystemConfig:ROMs:0:Name"] =
                    OricSystemConfig.SystemRomName,
                [$"{OricHeadlessHostConfig.ConfigSectionName}:SystemConfig:ROMs:0:File"] =
                    OricSystemConfig.AtmosRomFileName,
                [$"{OricHeadlessHostConfig.ConfigSectionName}:SystemConfig:AudioEnabled"] = "false",
            })
            .Build();
        var configurer = new OricSystemConfigurerCore(
            NullLoggerFactory.Instance,
            configuration,
            () => new OricHeadlessHostConfig(),
            OricHeadlessHostConfig.ConfigSectionName);

        var hostConfig = Assert.IsType<OricHeadlessHostConfig>(
            await configurer.GetNewHostSystemConfig());

        Assert.False(hostConfig.AudioSupported);
        Assert.False(hostConfig.SystemConfig.AudioEnabled);
        var rom = Assert.Single(hostConfig.SystemConfig.ROMs);
        Assert.Equal(OricSystemConfig.SystemRomName, rom.Name);
        Assert.Equal(OricSystemConfig.AtmosRomFileName, rom.File);
        Assert.Equal(OricSystemConfig.AtmosRomSha1, Assert.Single(rom.ValidVersionChecksums).Value);
    }

    [Fact]
    public async Task Headless_Plugin_Identifies_Oric_And_The_Atmos_Variant()
    {
        var plugin = new OricHeadlessEnginePlugin();
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton<IConfiguration>(configuration);
        plugin.Register(services, configuration);
        await using var serviceProvider = services.BuildServiceProvider();

        var configurer = Assert.IsType<OricSystemConfigurerCore>(
            serviceProvider.GetRequiredService<ISystemConfigurer>());

        Assert.Equal("Oric", plugin.SystemName);
        Assert.Equal("Headless", plugin.HostTechName);
        Assert.Equal(
            [OricSystemConfigurerCore.VariantAtmos48K],
            await configurer.GetConfigurationVariants(new OricSystemConfig()));
    }
}
