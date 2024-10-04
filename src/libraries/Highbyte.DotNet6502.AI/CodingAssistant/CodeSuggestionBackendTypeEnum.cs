using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.AI.CodingAssistant;

[JsonConverter(typeof(JsonStringEnumConverter<CodeSuggestionBackendTypeEnum>))]
public enum CodeSuggestionBackendTypeEnum
{
    None,
    /// <summary>
    /// Using OpenAI API.
    /// Requires an OpenAI API key.
    /// </summary>
    OpenAI,
    /// <summary>
    /// Using a self-hosted API (compatible with OpenAI) that provides the CodeLlama-code model.
    /// Does not require a OpenAI API key.
    /// May require a an API key to the self-hosted endpoint (if enabled there)..
    /// </summary>
    OpenAISelfHostedCodeLlama,
    /// <summary>
    /// A custom endpoint that in turn calls OpenAI (or a self-hosted API). 
    /// Does not require user to provide a OpenAI API key.
    /// May require a custom API key to the endpoint.
    /// </summary>
    CustomEndpoint
}
