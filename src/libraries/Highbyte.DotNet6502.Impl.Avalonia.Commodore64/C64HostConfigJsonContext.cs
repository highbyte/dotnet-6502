using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Impl.Avalonia.Commodore64;

/// <summary>
/// Source-generated JSON context for the C64 host config.
/// Kept separate from the Avalonia.Core context so this plug-in
/// can be added/removed without recompiling the core.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(C64HostConfig))]
internal partial class C64HostConfigJsonContext : JsonSerializerContext
{
}
