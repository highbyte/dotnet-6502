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
    JsonSerializable(typeof(Dictionary<string, string?>)),
    JsonSerializable(typeof(Dictionary<string, object?>)),
    JsonSerializable(typeof(LocalStorageScript)),
    JsonSerializable(typeof(List<LocalStorageScript>)),
]
internal partial class HostConfigJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Represents a Lua script entry loaded from browser localStorage.
/// JSON property names are lowercase to match what BrowserScripting.js returns.
/// </summary>
internal record LocalStorageScript(string name, string content);
