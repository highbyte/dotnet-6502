using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.AI.CodingAssistant;

public static class CodeSuggestionConfigurator
{
    public static ICodeSuggestion CreateCodeSuggestion(
        CodeSuggestionBackendTypeEnum codeSuggestionBackendType,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        string programmingLanguage,
        string additionalSystemInstruction,
        bool defaultToNoneIdConfigError = false)
    {
        ICodeSuggestion codeSuggestion;
        try
        {
            codeSuggestion = codeSuggestionBackendType switch
            {
                CodeSuggestionBackendTypeEnum.OpenAI => OpenAICodeSuggestion.CreateOpenAICodeSuggestion(configuration, loggerFactory, programmingLanguage, additionalSystemInstruction),
                CodeSuggestionBackendTypeEnum.OpenAISelfHostedCodeLlama => OpenAICodeSuggestion.CreateOpenAICodeSuggestionForCodeLlama(configuration, loggerFactory, programmingLanguage, additionalSystemInstruction),
                CodeSuggestionBackendTypeEnum.CustomEndpoint => new CustomAIEndpointCodeSuggestion(configuration, loggerFactory, programmingLanguage),
                CodeSuggestionBackendTypeEnum.None => new NoCodeSuggestion(),
                _ => throw new NotImplementedException($"CodeSuggestionBackendType '{codeSuggestionBackendType}' is not implemented.")
            };
        }
        catch (Exception ex)
        {
            if (defaultToNoneIdConfigError)
                codeSuggestion = new NoCodeSuggestion();
            else
                throw;
        }
        return codeSuggestion;
    }
}
