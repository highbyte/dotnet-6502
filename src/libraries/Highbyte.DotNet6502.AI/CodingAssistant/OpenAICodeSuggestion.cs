using Highbyte.DotNet6502.AI.CodingAssistant.Inference.OpenAI;
using Highbyte.DotNet6502.AI.CodingAssistant.Inference;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.AI.CodingAssistant;
public class OpenAICodeSuggestion : ICodeSuggestion
{
    private bool _isAvailable;
    private string? _lastError;
    private readonly OpenAIInferenceBackend _inferenceBackend;
    private readonly CodeCompletionConfig _codeCompletionConfig;
    private readonly CodeCompletionInference _codeCompletionInference;
    private readonly ILogger<OpenAICodeSuggestion> _logger;

    // OpenAI
    public static OpenAICodeSuggestion CreateOpenAICodeSuggestion(IConfiguration configuration, ILoggerFactory loggerFactory, string programmingLanguage, string additionalSystemInstruction = "")
    => CreateOpenAICodeSuggestion(new ApiConfig(configuration, selfHosted: false), loggerFactory, programmingLanguage, additionalSystemInstruction);
    public static OpenAICodeSuggestion CreateOpenAICodeSuggestion(ApiConfig apiConfig, ILoggerFactory loggerFactory, string programmingLanguage, string additionalSystemInstruction)
    {
        var codeCompletionConfig = CodeSuggestionSystemInstructions.GetOpenAICodeCompletionConfig(programmingLanguage, additionalSystemInstruction);
        return new OpenAICodeSuggestion(apiConfig, loggerFactory, codeCompletionConfig);
    }

    // CodeLlama via self-hosted OpenAI compatible API (Ollama)
    public static OpenAICodeSuggestion CreateOpenAICodeSuggestionForCodeLlama(IConfiguration configuration, ILoggerFactory loggerFactory, string programmingLanguage, string additionalSystemInstruction)
            => CreateOpenAICodeSuggestionForCodeLlama(new ApiConfig(configuration, selfHosted: true), loggerFactory, programmingLanguage, additionalSystemInstruction);
    public static OpenAICodeSuggestion CreateOpenAICodeSuggestionForCodeLlama(ApiConfig apiConfig, ILoggerFactory loggerFactory, string programmingLanguage, string additionalSystemInstruction)
    {
        var codeCompletionConfig = CodeSuggestionSystemInstructions.GetCodeLlamaCodeCompletionConfig(programmingLanguage, additionalSystemInstruction);
        return new OpenAICodeSuggestion(apiConfig, loggerFactory, codeCompletionConfig);
    }

    private OpenAICodeSuggestion(ApiConfig apiConfig, ILoggerFactory loggerFactory, CodeCompletionConfig codeCompletionConfig)
    {
        _isAvailable = true;
        _lastError = null;
        _inferenceBackend = new OpenAIInferenceBackend(apiConfig);
        _codeCompletionConfig = codeCompletionConfig;
        _codeCompletionInference = new CodeCompletionInference();

        _logger = loggerFactory.CreateLogger<OpenAICodeSuggestion>();
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
            _logger.LogError(ex, "Error getting code suggestion from OpenAI API.");
            _isAvailable = false;
            _lastError = ex.Message;

            return string.Empty;
        }
    }
}
