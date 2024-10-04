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

    public OpenAICodeSuggestion(IConfiguration configuration, string programmingLanguage, List<ChatMessage> examples)
        : this(new ApiConfig(configuration), programmingLanguage, examples)
    {
    }

    public OpenAICodeSuggestion(ApiConfig apiConfig, string programmingLanguage, List<ChatMessage> examples)
    {
        _isAvailable = true;
        _lastError = null;
        _inferenceBackend = new OpenAIInferenceBackend(apiConfig);

        // Workaround for examples seemingly messing up OpenAI inference, but is required for local Ollama models?
        List<ChatMessage> usingExamples = apiConfig.SelfHosted ? examples : new();

        _codeCompletionConfig = new CodeCompletionConfig { ProgrammingLanguage = programmingLanguage, Examples = usingExamples };
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
