using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Impl.Avalonia.Apple2;

/// <summary>
/// Source-generated JSON context for the Apple II host config.
/// Kept separate from the Avalonia.Core context so this plug-in
/// can be added/removed without recompiling the core.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Apple2HostConfig))]
internal partial class Apple2HostConfigJsonContext : JsonSerializerContext
{
}
