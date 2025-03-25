using System.Text.Json;
using System.Text;
using Microsoft.Extensions.AI;
using static Highbyte.DotNet6502.AI.CodingAssistant.CustomAIEndpointCodeSuggestion;

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference.BackendConfig;

public class CustomAIEndpointChatClient : IChatClient
{
    private readonly CustomAIEndpointConfig _apiWrapperConfig;
    private readonly string _programmingLanguage;
    private readonly HttpClient _httpClient;

    public CustomAIEndpointChatClient(CustomAIEndpointConfig apiWrapperConfig, string programmingLanguage)
    {
        _apiWrapperConfig = apiWrapperConfig;
        _programmingLanguage = programmingLanguage;

        // TODO: HttpClient should be injected, not created each time this class is created.
        _httpClient = new HttpClient
        {
            BaseAddress = apiWrapperConfig.Endpoint
        };
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        // TODO: When using the custom API in this way (from a IChatClient implementation), the API that is called should probably have a new endpoint that is more of a pass-through of Messages instead of expecting textBefore, textAfter, and programming language as separate parameters.
        string codeMessage = messages.Last().Text;
        // Format of codeMesage is: [textBefore]^^^[textAfter]
        var split = codeMessage.Split("^^^");
        string textBefore = split[0];
        string textAfter = split[1];

        string response = await GetInsertSuggestionAsyncInternal(textBefore, textAfter);
        return new ChatResponse
        {
            Messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, response)
            }
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
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
                "application/json"),

            Headers = { { "x-api-key", _apiWrapperConfig.ApiKey } }
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

}
