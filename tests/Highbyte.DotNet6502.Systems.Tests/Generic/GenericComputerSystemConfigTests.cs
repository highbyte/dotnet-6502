using System.Text.Json;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Systems.Generic.Render;

namespace Highbyte.DotNet6502.Systems.Tests.Generic;

public class GenericComputerSystemConfigTests
{
    [Fact]
    public void Serialize_Persists_Type_Names_Without_Assembly_Version_Metadata()
    {
        var config = new GenericComputerSystemConfig();
        config.SetRenderProviderType(typeof(GenericVideoCommandStream));
        config.SetRenderTargetType(typeof(TestRenderTarget));

        var json = JsonSerializer.Serialize(config);

        AssertPersistedType(json, "RenderProviderType", typeof(GenericVideoCommandStream));
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
              "RenderProviderType": "{{LegacyAssemblyQualifiedName(typeof(GenericVideoCommandStream))}}",
              "RenderTargetType": "{{LegacyAssemblyQualifiedName(typeof(TestRenderTarget))}}"
            }
            """;

        var config = JsonSerializer.Deserialize<GenericComputerSystemConfig>(json)!;

        Assert.Equal(typeof(GenericVideoCommandStream), config.RenderProviderType);
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

    private sealed class TestRenderTarget
    {
    }
}
