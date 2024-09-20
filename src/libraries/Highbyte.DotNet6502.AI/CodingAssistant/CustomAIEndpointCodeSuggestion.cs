using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text;

namespace Highbyte.DotNet6502.AI.CodingAssistant;

/// <summary>
/// Get code completion via a custom AI wrapper endpoint without requiring OpenAI API key (or similar) from the calling application.
/// </summary>
public class CustomAIEndpointCodeSuggestion : ICodeSuggestion
{
    private bool _isAvailable;
    private string? _lastError;
    private readonly string _programmingLanguage;
    private readonly HttpClient _httpClient;

    public CustomAIEndpointCodeSuggestion(IConfiguration configuration, string programmingLanguage)
        : this(new CustomAIEndpointConfig(configuration), programmingLanguage)
    {
    }

    public CustomAIEndpointCodeSuggestion(CustomAIEndpointConfig apiWrapperConfig, string programmingLanguage)
    {
        _isAvailable = true;
        _lastError = null;

        _programmingLanguage = programmingLanguage;
        // TODO: HttpClient should be injected, not created each time this class is created.
        _httpClient = new HttpClient
        {
            BaseAddress = apiWrapperConfig.Endpoint
        };
    }

    /// <summary>
    /// Check connection and set IsAvailable and LastError base on result.
    /// </summary>
    /// <returns></returns>
    public async Task CheckAvailability()
    {
        var dummy = await GetInsertionSuggestionAsync("test", "test");
    }

    public bool IsAvailable => _isAvailable;
    public string? LastError => _lastError;

    public virtual async Task<string> GetInsertionSuggestionAsync(string textBefore, string textAfter)
    {
        try
        {
            return await GetInsertSuggestionAsyncInternal(textBefore, textAfter);
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _lastError = ex.Message;
            return string.Empty;
        }
    }

    private async Task<string> GetInsertSuggestionAsyncInternal(string textBefore, string textAfter)
    {
        // Use custom endpoint
        var request = new HttpRequestMessage(HttpMethod.Post, "CodeCompletionProxy")
        {
            Content = new StringContent(JsonSerializer.Serialize(
                new CodeCompletionRequest
                {
                    ProgrammingLanguage = _programmingLanguage,
                    TextBefore = textBefore,
                    TextAfter = textAfter
                }),
                Encoding.UTF8,
                "application/json")
        };
        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to get code completion suggestion from custom AI endpoint. Status code: {response.StatusCode}, Content: {responseContent}");
        }

        var responseTyped = JsonSerializer.Deserialize<CodeCompletionResponse>(
            responseContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return responseTyped?.CodeInsertion ?? string.Empty;

    }

    public class CustomAIEndpointConfig
    {
        public string? ApiKey { get; set; }

        public Uri? Endpoint { get; set; }

        public const string CONFIG_SECTION = "CodingAssistant:CustomEndpoint";

        public CustomAIEndpointConfig()
        {
        }

        public CustomAIEndpointConfig(IConfiguration config)
        {
            var configSection = config.GetRequiredSection(CONFIG_SECTION);

            Endpoint = configSection.GetValue<Uri>("Endpoint")
                ?? throw new InvalidOperationException($"Missing required configuration value: {CONFIG_SECTION}:Endpoint.");

            ApiKey = configSection.GetValue<string>("ApiKey")
                ?? throw new InvalidOperationException($"Missing required configuration value: {CONFIG_SECTION}:ApiKey");
        }
    }

    class CodeCompletionRequest
    {
        public required string ProgrammingLanguage { get; set; }
        public required string TextBefore { get; set; }
        public required string TextAfter { get; set; }
    }

    class CodeCompletionResponse
    {
        public string? CodeInsertion { get; set; }
    }
}
