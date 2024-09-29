// Based on https://github.com/dotnet/smartcomponents

using System.Runtime.InteropServices;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference.OpenAI;

public class OpenAIInferenceBackend(ApiConfig apiConfig)
    : IInferenceBackend
{
    public OpenAIInferenceBackend(IConfiguration configuration) : this(new ApiConfig(configuration))
    {
    }

    public async Task<string> GetChatResponseAsync(ChatParameters options)
    {
#if DEBUG
        if (RuntimeInformation.OSArchitecture != Architecture.Wasm)
        {
            if (ResponseCache.TryGetCachedResponse(options, out var cachedResponse))
                return cachedResponse!;
        }
#endif

        var client = CreateClient(apiConfig);
        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = apiConfig.DeploymentName,
            Temperature = options.Temperature ?? 0f,
            NucleusSamplingFactor = options.TopP ?? 1,
            MaxTokens = options.MaxTokens ?? 200,
            FrequencyPenalty = options.FrequencyPenalty ?? 0,
            PresencePenalty = options.PresencePenalty ?? 0,
        };

        foreach (var message in options.Messages ?? Enumerable.Empty<ChatMessage>())
        {
            chatCompletionsOptions.Messages.Add(message.Role switch
            {
                ChatMessageRole.System => new ChatRequestSystemMessage(message.Text),
                ChatMessageRole.User => new ChatRequestUserMessage(message.Text),
                ChatMessageRole.Assistant => new ChatRequestAssistantMessage(message.Text),
                _ => throw new InvalidOperationException($"Unknown chat message role: {message.Role}")
            });
        }

        if (options.StopSequences is { } stopSequences)
        {
            foreach (var stopSequence in stopSequences)
            {
                chatCompletionsOptions.StopSequences.Add(stopSequence);
            }
        }

        var completionsResponse = await client.GetChatCompletionsAsync(chatCompletionsOptions);

        var response = completionsResponse.Value.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;

#if DEBUG
        if (RuntimeInformation.OSArchitecture != Architecture.Wasm)
        {
            ResponseCache.SetCachedResponse(options, response);
        }
#endif

        return response;
    }

    private static OpenAIClient CreateClient(ApiConfig apiConfig)
    {
        if (apiConfig.SelfHosted)
        {
            var transport = new SelfHostedLlmTransport(apiConfig.Endpoint!);
            return new OpenAIClient(apiConfig.ApiKey, new() { Transport = transport });
        }
        else if (apiConfig.Endpoint is null)
        {
            // OpenAI
            return new OpenAIClient(apiConfig.ApiKey);
        }
        else
        {
            // Azure OpenAI
            return new OpenAIClient(
                apiConfig.Endpoint,
                new AzureKeyCredential(apiConfig.ApiKey!));
        }
    }
}
