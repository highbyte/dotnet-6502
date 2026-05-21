using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Impl.Avalonia.Generic;

/// <summary>
/// Source-generated JSON context for the Generic computer host config.
/// Kept in the plug-in so this system can be added/removed without recompiling the core.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GenericComputerHostConfig))]
internal partial class GenericComputerHostConfigJsonContext : JsonSerializerContext
{
}
