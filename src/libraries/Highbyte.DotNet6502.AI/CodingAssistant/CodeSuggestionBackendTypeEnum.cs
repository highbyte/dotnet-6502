using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.AI.CodingAssistant;

[JsonConverter(typeof(JsonStringEnumConverter<CodeSuggestionBackendTypeEnum>))]
public enum CodeSuggestionBackendTypeEnum
{
    None,
    OpenAI,
    CustomEndpoint
}
