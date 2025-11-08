using System.Collections.Generic;
using System.Text.Json;

internal static class JsonHelper
{
    /// <summary>
    /// Flattens a JsonElement to a dictionary suitable for IConfiguration.
    /// Configuration keys use colon notation for hierarchy (e.g., "Section:SubSection:Key").
    /// This method handles any JSON structure including multiple root sections.
    /// </summary>
    internal static void FlattenJsonElementToDictionary(JsonElement element, string prefix, Dictionary<string, string?> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = $"{prefix}:{property.Name}";
                    FlattenJsonElementToDictionary(property.Value, key, result);
                }
                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var key = $"{prefix}:{index}";
                    FlattenJsonElementToDictionary(item, key, result);
                    index++;
                }
                break;

            case JsonValueKind.String:
                result[prefix] = element.GetString();
                break;

            case JsonValueKind.Number:
                result[prefix] = element.GetRawText();
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                result[prefix] = element.GetBoolean().ToString();
                break;

            case JsonValueKind.Null:
                result[prefix] = null;
                break;

            case JsonValueKind.Undefined:
                // Skip undefined values
                break;
        }
    }
}
