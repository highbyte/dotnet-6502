using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// Source generation context for host config JSON serialization.
/// Makes it AOT friendly and faster.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true // your option from the code
)]
[JsonSerializable(typeof(EmulatorConfig))]
internal partial class EmulatorConfigJsonContext : JsonSerializerContext
{
}