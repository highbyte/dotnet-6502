using System.Text.Json;
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

    [Fact]
    public void Serialize_Persists_Type_Names_Without_Assembly_Version_Metadata()
    {
        var config = new Vic20SystemConfig();
        config.SetRenderProviderType(typeof(Vic20VideoCommandStream));
        config.SetRenderTargetType(typeof(TestRenderTarget));

        var json = JsonSerializer.Serialize(config);

        AssertPersistedType(json, "RenderProviderType", typeof(Vic20VideoCommandStream));
        AssertPersistedType(json, "RenderTargetType", typeof(TestRenderTarget));
        Assert.DoesNotContain("Version=", json);
        Assert.DoesNotContain("Culture=", json);
        Assert.DoesNotContain("PublicKeyToken=", json);
    }

    [Fact]
    public void Deserialize_Accepts_Legacy_Assembly_Qualified_Type_Names()
    {
        var json = $$"""
            {
              "RenderProviderType": "{{LegacyAssemblyQualifiedName(typeof(Vic20VideoCommandStream))}}",
              "RenderTargetType": "{{LegacyAssemblyQualifiedName(typeof(TestRenderTarget))}}"
            }
            """;

        var config = JsonSerializer.Deserialize<Vic20SystemConfig>(json)!;

        Assert.Equal(typeof(Vic20VideoCommandStream), config.RenderProviderType);
        Assert.Equal(typeof(TestRenderTarget), config.RenderTargetType);
    }

    private static string LegacyAssemblyQualifiedName(Type type)
        => $"{type.FullName}, {type.Assembly.GetName().Name}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";

    private static void AssertPersistedType(string json, string propertyName, Type expectedType)
    {
        using var document = JsonDocument.Parse(json);
        var actual = document.RootElement.GetProperty(propertyName).GetString();

        Assert.Equal($"{expectedType.FullName}, {expectedType.Assembly.GetName().Name}", actual);
    }

    private sealed class TestVic20HostConfig : HostSystemConfigBase<Vic20SystemConfig>
    {
        public override bool AudioSupported => false;
    }

    private sealed class TestRenderTarget
    {
    }
}
