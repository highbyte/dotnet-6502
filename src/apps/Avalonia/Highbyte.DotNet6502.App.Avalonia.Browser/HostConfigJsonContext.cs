using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Source generation context for host config JSON serialization.
/// Makes it AOT friendly and faster.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true // your option from the code
)]
[
    JsonSerializable(typeof(JsonElement)),
    JsonSerializable(typeof(Dictionary<string, JsonElement>)),
]
internal partial class HostConfigJsonContext : JsonSerializerContext
{
}
