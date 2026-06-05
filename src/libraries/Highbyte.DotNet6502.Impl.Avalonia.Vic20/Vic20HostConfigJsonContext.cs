using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Impl.Avalonia.Vic20;

/// <summary>
/// Source-generated JSON context for the VIC-20 host config.
/// Kept separate from the Avalonia.Core context so this plug-in
/// can be added/removed without recompiling the core.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Vic20HostConfig))]
internal partial class Vic20HostConfigJsonContext : JsonSerializerContext
{
}
