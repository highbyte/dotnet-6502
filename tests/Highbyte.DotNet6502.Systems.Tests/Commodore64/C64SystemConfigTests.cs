using System.Text.Json;
using Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Render.VideoCommands;

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

    [Fact]
    public void Serialize_Persists_Type_Names_Without_Assembly_Version_Metadata()
    {
        var config = new C64SystemConfig();
        config.SetRenderProviderType(typeof(C64VideoCommandStream));
        config.SetRenderTargetType(typeof(TestRenderTarget));
        config.SetAudioProviderType(typeof(C64SidSampleProvider));
        config.SetAudioTargetType(typeof(TestAudioTarget));

        var json = JsonSerializer.Serialize(config);

        AssertPersistedType(json, "RenderProviderType", typeof(C64VideoCommandStream));
        AssertPersistedType(json, "RenderTargetType", typeof(TestRenderTarget));
        AssertPersistedType(json, "AudioProviderType", typeof(C64SidSampleProvider));
        AssertPersistedType(json, "AudioTargetType", typeof(TestAudioTarget));
        Assert.DoesNotContain("Version=", json);
        Assert.DoesNotContain("Culture=", json);
        Assert.DoesNotContain("PublicKeyToken=", json);
    }

    [Fact]
    public void Deserialize_Accepts_Legacy_Assembly_Qualified_Type_Names()
    {
        var json = $$"""
            {
              "RenderProviderType": "{{LegacyAssemblyQualifiedName(typeof(C64VideoCommandStream))}}",
              "RenderTargetType": "{{LegacyAssemblyQualifiedName(typeof(TestRenderTarget))}}",
              "AudioProviderType": "{{LegacyAssemblyQualifiedName(typeof(C64SidSampleProvider))}}",
              "AudioTargetType": "{{LegacyAssemblyQualifiedName(typeof(TestAudioTarget))}}"
            }
            """;

        var config = JsonSerializer.Deserialize<C64SystemConfig>(json)!;

        Assert.Equal(typeof(C64VideoCommandStream), config.RenderProviderType);
        Assert.Equal(typeof(TestRenderTarget), config.RenderTargetType);
        Assert.Equal(typeof(C64SidSampleProvider), config.AudioProviderType);
        Assert.Equal(typeof(TestAudioTarget), config.AudioTargetType);
    }

    private static string LegacyAssemblyQualifiedName(Type type)
        => $"{type.FullName}, {type.Assembly.GetName().Name}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";

    private static void AssertPersistedType(string json, string propertyName, Type expectedType)
    {
        using var document = JsonDocument.Parse(json);
        var actual = document.RootElement.GetProperty(propertyName).GetString();

        Assert.Equal($"{expectedType.FullName}, {expectedType.Assembly.GetName().Name}", actual);
    }

    private sealed class TestRenderTarget
    {
    }

    private sealed class TestAudioTarget
    {
    }
}
