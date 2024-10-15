using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference.BackendConfig;

public static class ChatClientHelper
{
    public static IChatClient CreateChatClient(CodeCompletionBackendType codeCompletionBackendType, IConfiguration config)
    {
        var client = codeCompletionBackendType switch
        {
            CodeCompletionBackendType.OpenAI => CreateOpenAIChatClient(new OpenAIConfig(config)),
            CodeCompletionBackendType.AzureOpenAI => CreateAzureOpenAIChatClient(new AzureOpenAIConfig(config)),
            CodeCompletionBackendType.Ollama => CreateOllamaChatClient(new OllamaConfig(config)),
            _ => throw new InvalidOperationException($"Invalid backend type: {codeCompletionBackendType}")
        };
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
}
