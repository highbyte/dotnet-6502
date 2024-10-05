using Highbyte.DotNet6502.AI.CodingAssistant.Inference.OpenAI;
using Highbyte.DotNet6502.AI.CodingAssistant.Inference;
using Microsoft.Extensions.Configuration;

namespace Highbyte.DotNet6502.AI.CodingAssistant;
public class OpenAICodeSuggestion : ICodeSuggestion
{
    private bool _isAvailable;
    private string? _lastError;
    private readonly OpenAIInferenceBackend _inferenceBackend;
    private readonly CodeCompletionConfig _codeCompletionConfig;
    private readonly CodeCompletionInference _codeCompletionInference;

    // OpenAI
    public static OpenAICodeSuggestion CreateOpenAICodeSuggestion(IConfiguration configuration, string programmingLanguage, string additionalSystemInstruction = "")
    => CreateOpenAICodeSuggestion(new ApiConfig(configuration, selfHosted: false), programmingLanguage, additionalSystemInstruction);
    public static OpenAICodeSuggestion CreateOpenAICodeSuggestion(ApiConfig apiConfig, string programmingLanguage, string additionalSystemInstruction)
    {
        var codeCompletionConfig = CodeSuggestionSystemInstructions.GetOpenAICodeCompletionConfig(programmingLanguage, additionalSystemInstruction);
        return new OpenAICodeSuggestion(apiConfig, codeCompletionConfig);
    }

    // CodeLlama via self-hosted OpenAI compatible API (Ollama)
    public static OpenAICodeSuggestion CreateOpenAICodeSuggestionForCodeLlama(IConfiguration configuration, string programmingLanguage, string additionalSystemInstruction)
            => CreateOpenAICodeSuggestionForCodeLlama(new ApiConfig(configuration, selfHosted: true), programmingLanguage, additionalSystemInstruction);
    public static OpenAICodeSuggestion CreateOpenAICodeSuggestionForCodeLlama(ApiConfig apiConfig, string programmingLanguage, string additionalSystemInstruction)
    {
        var codeCompletionConfig = CodeSuggestionSystemInstructions.GetCodeLlamaCodeCompletionConfig(programmingLanguage, additionalSystemInstruction);
        return new OpenAICodeSuggestion(apiConfig, codeCompletionConfig);
    }

    private OpenAICodeSuggestion(ApiConfig apiConfig, CodeCompletionConfig codeCompletionConfig)
    {
        _isAvailable = true;
        _lastError = null;
        _inferenceBackend = new OpenAIInferenceBackend(apiConfig);
        _codeCompletionConfig = codeCompletionConfig;
        _codeCompletionInference = new CodeCompletionInference();
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
            return await _codeCompletionInference.GetInsertionSuggestionAsync(_inferenceBackend, _codeCompletionConfig, textBefore, textAfter);
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _lastError = ex.Message;
            return string.Empty;
        }
    }
}
