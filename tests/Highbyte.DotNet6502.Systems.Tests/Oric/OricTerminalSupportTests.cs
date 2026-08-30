using Highbyte.DotNet6502.Impl.Terminal.Oric;
using Highbyte.DotNet6502.Systems.Oric;
using Highbyte.DotNet6502.Systems.Oric.Config;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Highbyte.DotNet6502.Systems.Oric.Render;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricTerminalSupportTests
{
    [Fact]
    public void Terminal_Config_Selects_Text_Rendering_And_Disables_Audio()
    {
        var config = new OricTerminalHostConfig();

        Assert.False(config.AudioSupported);
        Assert.False(config.SystemConfig.AudioEnabled);
        Assert.Equal(typeof(OricVideoCommandStream), config.SystemConfig.RenderProviderType);
    }

    [Fact]
    public async Task Terminal_Setup_Wires_Oric_Keyboard_And_Joystick_Input()
    {
        var setup = new OricTerminalSetup(
            NullLoggerFactory.Instance,
            new ConfigurationBuilder().Build());
        var hostConfig = new OricTerminalHostConfig();
        hostConfig.InputConfig.CurrentJoystick = 2;
        var oric = new OricMachine();

        _ = await setup.BuildSystemRunner(oric, hostConfig);

        var input = Assert.IsType<OricInputHandler>(oric.InputConsumer);
        Assert.Same(hostConfig.InputConfig, input.InputConfig);
        Assert.Equal(2, input.InputConfig.CurrentJoystick);
    }

    [Fact]
    public async Task Terminal_Plugin_Registers_The_Atmos_Configurer()
    {
        var plugin = new OricTerminalEnginePlugin();
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton<IConfiguration>(configuration);
        plugin.Register(services, configuration);
        await using var serviceProvider = services.BuildServiceProvider();

        var configurer = Assert.IsType<OricTerminalSetup>(
            serviceProvider.GetRequiredService<ISystemConfigurer>());

        Assert.Equal(OricMachine.SystemName, plugin.SystemName);
        Assert.Equal("Terminal", plugin.HostTechName);
        Assert.Equal(
            [OricSystemConfigurerCore.VariantAtmos48K],
            await configurer.GetConfigurationVariants(new OricSystemConfig()));
    }
}
