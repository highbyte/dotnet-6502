using Highbyte.DotNet6502.AI.CodingAssistant.Inference;
using Highbyte.DotNet6502.AI.CodingAssistant.Inference.BackendConfig;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace Highbyte.DotNet6502.AI.CodingAssistant;
public class OpenAICodeSuggestion : ICodeSuggestion
{
    private bool _isAvailable;
    private string? _lastError;
    private readonly CodeCompletionConfig _codeCompletionConfig;
    private readonly CodeCompletionInference _codeCompletionInference;

    // OpenAI
    public static OpenAICodeSuggestion CreateOpenAICodeSuggestion(IConfiguration configuration, string programmingLanguage, string additionalSystemInstruction = "")
        => CreateOpenAICodeSuggestion(ChatClientFactory.CreateChatClient(CodeCompletionBackendType.OpenAI, configuration), programmingLanguage, additionalSystemInstruction);
    public static OpenAICodeSuggestion CreateOpenAICodeSuggestion(IChatClient chatClient, string programmingLanguage, string additionalSystemInstruction)
    {
        var codeCompletionConfig = CodeSuggestionSystemInstructions.GetOpenAICodeCompletionConfig(programmingLanguage, additionalSystemInstruction);
        return new OpenAICodeSuggestion(chatClient, codeCompletionConfig);
    }

    // CodeLlama via self-hosted OpenAI compatible API (Ollama)
    public static OpenAICodeSuggestion CreateOpenAICodeSuggestionForCodeLlama(IConfiguration configuration, string programmingLanguage, string additionalSystemInstruction)
            => CreateOpenAICodeSuggestionForCodeLlama(ChatClientFactory.CreateChatClient(CodeCompletionBackendType.Ollama, configuration), programmingLanguage, additionalSystemInstruction);
    public static OpenAICodeSuggestion CreateOpenAICodeSuggestionForCodeLlama(IChatClient chatClient, string programmingLanguage, string additionalSystemInstruction)
    {
        var codeCompletionConfig = CodeSuggestionSystemInstructions.GetCodeLlamaCodeCompletionConfig(programmingLanguage, additionalSystemInstruction);
        return new OpenAICodeSuggestion(chatClient, codeCompletionConfig);
    }

    private OpenAICodeSuggestion(IChatClient chatClient, CodeCompletionConfig codeCompletionConfig)
    {
        _isAvailable = true;
        _lastError = null;
        _codeCompletionConfig = codeCompletionConfig;
        _codeCompletionInference = new CodeCompletionInference(chatClient);
    }

    public bool IsAvailable => _isAvailable;
    public string? LastError => _lastError;

    public async Task CheckAvailability()
    {
        var dummy = await GetInsertionSuggestionAsync("test", "test");
    }

    public virtual async Task<string> GetInsertionSuggestionAsync(string textBefore, string textAfter)
    {
        // Call OpenAI API directly
        try
        {
            return await _codeCompletionInference.GetInsertionSuggestionAsync(_codeCompletionConfig, textBefore, textAfter);
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _lastError = ex.Message;
            return string.Empty;
        }
    }
}
