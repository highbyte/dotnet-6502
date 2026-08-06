using Highbyte.DotNet6502.Systems.Apple2;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Render;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Apple2SystemConfigTests
{
    [Fact]
    public void The_Rasterizer_Is_The_Default_Render_Provider()
    {
        var config = new Apple2SystemConfig();

        Assert.Equal(typeof(Apple2Rasterizer), config.RenderProviderType);
        Assert.Contains(typeof(Apple2VideoCommandStream), config.GetSupportedRenderProviderTypes());
    }

    [Fact]
    public void Setting_An_Unsupported_Render_Provider_Is_Rejected()
    {
        var config = new Apple2SystemConfig();

        Assert.Throws<DotNet6502Exception>(() => config.SetRenderProviderType(typeof(Apple2SystemConfigTests)));
    }

    [Fact]
    public void The_System_Has_No_Audio()
    {
        var config = new Apple2SystemConfig();

        Assert.False(config.AudioEnabled);
        Assert.Empty(config.GetSupportedAudioProviderTypes());
        Assert.Null(config.AudioProviderType);
        Assert.Throws<DotNet6502Exception>(() => config.SetAudioProviderType(typeof(object)));
        Assert.Throws<DotNet6502Exception>(() => config.SetAudioTargetType(typeof(object)));
    }

    [Fact]
    public void A_Config_Without_The_System_Rom_Is_Invalid()
    {
        var config = new Apple2SystemConfig();

        Assert.False(config.IsValid(out var errors));
        Assert.Contains(errors, e => e.Contains(Apple2SystemConfig.SYSTEM_ROM_NAME));
    }

    [Fact]
    public void Both_The_System_Rom_And_The_Character_Generator_Are_Required()
    {
        Assert.Contains(Apple2SystemConfig.SYSTEM_ROM_NAME, Apple2SystemConfig.RequiredROMs);
        Assert.Contains(Apple2SystemConfig.CHARGEN_ROM_NAME, Apple2SystemConfig.RequiredROMs);
    }

    [Fact]
    public void A_Config_Without_The_Character_Generator_Is_Invalid()
    {
        var config = new Apple2SystemConfig();
        config.SetROM(Apple2SystemConfig.SYSTEM_ROM_NAME, file: "apple.rom");

        Assert.False(config.IsValid(out var errors));
        Assert.Contains(errors, e => e.Contains(Apple2SystemConfig.CHARGEN_ROM_NAME));
    }

    [Fact]
    public void SetROM_Applies_The_Known_Character_Generator_Checksum()
    {
        var config = new Apple2SystemConfig();
        config.SetROM(Apple2SystemConfig.CHARGEN_ROM_NAME, data: new byte[2048]);

        var rom = config.GetROM(Apple2SystemConfig.CHARGEN_ROM_NAME);

        Assert.Contains("f9d312f128c9557d9d6ac03bfad6c3ddf83e5659", rom.ValidVersionChecksums.Values);
    }

    [Fact]
    public void SetROM_Applies_The_Known_System_Rom_Checksums()
    {
        var config = new Apple2SystemConfig();
        config.SetROM(Apple2SystemConfig.SYSTEM_ROM_NAME, data: new byte[Apple2System.SystemRomSize]);

        var rom = config.GetROM(Apple2SystemConfig.SYSTEM_ROM_NAME);

        // Both the trimmed 12 KB image and the 20 KB $B000-$FFFF layout are accepted.
        Assert.Equal(2, rom.ValidVersionChecksums.Count);
        Assert.Contains("8c5ca0c39005dfb0898af2c0992f797cc77530c0", rom.ValidVersionChecksums.Values);
        Assert.Contains("29a53f3bb158b160433369e8e4a1d7cd5bf68ac6", rom.ValidVersionChecksums.Values);
    }

    [Fact]
    public void A_Rom_With_The_Wrong_Contents_Fails_Validation()
    {
        var config = new Apple2SystemConfig { ROMDirectory = "." };
        config.SetROM(Apple2SystemConfig.SYSTEM_ROM_NAME, data: new byte[Apple2System.SystemRomSize]);
        config.SetROM(Apple2SystemConfig.CHARGEN_ROM_NAME, data: new byte[2048]);

        Assert.False(config.IsValid(out var errors));
        Assert.Contains(errors, e => e.Contains("checksum"));
    }

    [Fact]
    public void Clone_Deep_Copies_The_Rom_List()
    {
        var config = new Apple2SystemConfig();
        config.SetROM(Apple2SystemConfig.SYSTEM_ROM_NAME, file: "apple.rom");

        var clone = (Apple2SystemConfig)config.Clone();
        clone.GetROM(Apple2SystemConfig.SYSTEM_ROM_NAME).File = "OTHER.ROM";

        Assert.Equal("apple.rom", config.GetROM(Apple2SystemConfig.SYSTEM_ROM_NAME).File);
    }

    [Fact]
    public void Changing_A_Setting_Marks_The_Config_Dirty()
    {
        var config = new Apple2SystemConfig();
        config.ClearDirty();

        config.MonitorColor = Apple2MonitorColor.Amber;

        Assert.True(config.IsDirty);
        config.ClearDirty();
        Assert.False(config.IsDirty);
    }

    [Fact]
    public async Task Configurer_Reports_The_System_Name_And_Its_Configuration_Variants()
    {
        var configurer = BuildConfigurer(new ConfigurationBuilder().Build());

        Assert.Equal(Apple2System.SystemName, configurer.SystemName);
        Assert.Equal(
            new List<string> { Apple2SystemConfigurerCore.VariantApple2Plus },
            await configurer.GetConfigurationVariants(new Apple2SystemConfig()));
    }

    [Fact]
    public void Configurer_Reports_Screen_Geometry_Without_Building_The_System()
    {
        var configurer = BuildConfigurer(new ConfigurationBuilder().Build());

        var screen = configurer.GetScreenInfo(Apple2SystemConfigurerCore.VariantApple2Plus, new Apple2SystemConfig());

        Assert.NotNull(screen);
        Assert.Equal(Apple2Config.DrawableAreaWidth, screen.DrawableAreaWidth);
        Assert.Equal(Apple2Config.DrawableAreaHeight, screen.DrawableAreaHeight);
        Assert.False(screen.HasBorder);
    }

    [Fact]
    public async Task Configurer_Builds_A_System_And_Applies_The_Display_Settings()
    {
        var configurer = BuildConfigurer(new ConfigurationBuilder().Build());
        var systemConfig = new Apple2SystemConfig { MonitorColor = Apple2MonitorColor.Amber };

        var system = await configurer.BuildSystem(Apple2SystemConfigurerCore.VariantApple2Plus, systemConfig);

        var apple2 = Assert.IsType<Apple2System>(system);
        Assert.Equal(Apple2MonitorColor.Amber, apple2.Apple2Config.MonitorColor);
        Assert.IsType<Apple2Rasterizer>(apple2.RenderProvider);
        Assert.Equal(Apple2Config.Cols, apple2.TextCols);
        Assert.Equal(Apple2Config.Rows, apple2.TextRows);
    }

    [Fact]
    public async Task Configurer_Applies_Render_Type_Overrides_From_Configuration()
    {
        var renderProviderType = typeof(Apple2VideoCommandStream);
        var renderTargetType = typeof(TestRenderTarget);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Apple2:SystemConfig:RenderProviderType"] = renderProviderType.AssemblyQualifiedName,
                ["Apple2:SystemConfig:RenderTargetType"] = renderTargetType.AssemblyQualifiedName
            }).Build();

        var configurer = BuildConfigurer(configuration);

        var hostConfig = (TestApple2HostConfig)await configurer.GetNewHostSystemConfig();

        Assert.Equal(renderProviderType, hostConfig.SystemConfig.RenderProviderType);
        Assert.Equal(renderTargetType, hostConfig.SystemConfig.RenderTargetType);
    }

    [Fact]
    public void Configurer_Exposes_The_Rom_Directory_As_User_Content()
    {
        var configurer = BuildConfigurer(new ConfigurationBuilder().Build());

        Assert.Contains(Apple2SystemConfig.DefaultROMDirectory, configurer.GetUserContentDirectories());
    }

    private static Apple2SystemConfigurerCore BuildConfigurer(IConfiguration configuration)
        => new(NullLoggerFactory.Instance, configuration, () => new TestApple2HostConfig(), "Apple2");

    private sealed class TestApple2HostConfig : HostSystemConfigBase<Apple2SystemConfig>
    {
        public override bool AudioSupported => false;
    }

    private sealed class TestRenderTarget
    {
    }
}
