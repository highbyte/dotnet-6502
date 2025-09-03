using System.Runtime.InteropServices;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenAI;
using static Highbyte.DotNet6502.AI.CodingAssistant.CustomAIEndpointCodeSuggestion;

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference.BackendConfig;

public static class ChatClientFactory
{
    public static IChatClient CreateChatClient(CodeCompletionBackendType codeCompletionBackendType, IConfiguration config, string programmingLanguage)
    {
        var chatClient = codeCompletionBackendType switch
        {
            CodeCompletionBackendType.OpenAI => CreateOpenAIChatClient(new OpenAIConfig(config)),
            CodeCompletionBackendType.AzureOpenAI => CreateAzureOpenAIChatClient(new AzureOpenAIConfig(config)),
            CodeCompletionBackendType.Ollama => CreateOllamaChatClient(new OllamaConfig(config)),
            CodeCompletionBackendType.CustomEndpoint => CreateCustomAIEndpointChatClient(new CustomAIEndpointConfig(config), programmingLanguage),
            _ => throw new InvalidOperationException($"Invalid backend type: {codeCompletionBackendType}")
        };

        // Skip DistributedCache caching for chat client, it doesn't work in wasm.
        if (RuntimeInformation.OSArchitecture == Architecture.Wasm)
        {
            return chatClient;
        }

        // Use DistributedCache caching
        var options = Options.Create(new MemoryDistributedCacheOptions
        {
            SizeLimit = 30 * 1024 * 1024    // Size in bytes
        });
        IDistributedCache cache = new MemoryDistributedCache(options);

        IChatClient client = new ChatClientBuilder(chatClient)
                        .UseDistributedCache(cache)
                        .Build();
        return client;
    }

    public static IChatClient CreateOpenAIChatClient(OpenAIConfig openAIConfig)
    {
        return new OpenAIClient(openAIConfig.ApiKey)
                    .AsChatClient(modelId: openAIConfig.ModelName);
    }

    public static IChatClient CreateAzureOpenAIChatClient(AzureOpenAIConfig azureOpenAIConfig)
    {
        return new AzureOpenAIClient(
               azureOpenAIConfig.Endpoint,
               new DefaultAzureCredential())
                   .AsChatClient(modelId: azureOpenAIConfig.ModelName);
    }

    public static IChatClient CreateOllamaChatClient(OllamaConfig ollamaConfig)
    {
        // TODO: Is custom httpClient with DisableActivityHandler needed (CORS fix for web client) when using Microsoft.Extensions.AI.Abstractions?
        //var httpClientHandler = new HttpClientHandler();
        //var disableActivityHandler = new DisableActivityHandler(httpClientHandler);
        //var httpClient = new HttpClient(disableActivityHandler);
        //return new OllamaChatClient(ollamaConfig.Endpoint, ollamaConfig.ModelName, httpClient);

        return new OllamaChatClient(ollamaConfig.Endpoint, ollamaConfig.ModelName);
    }

    public static IChatClient CreateCustomAIEndpointChatClient(CustomAIEndpointConfig customAIEndpointConfig, string programmingLanguage)
    {
        return new CustomAIEndpointChatClient(customAIEndpointConfig, programmingLanguage);
    }
}
