using Microsoft.Extensions.Configuration;

namespace Highbyte.DotNet6502.AI.CodingAssistant;

public static class CodeSuggestionConfigurator
{
    public static ICodeSuggestion CreateCodeSuggestion(
        CodeSuggestionBackendTypeEnum codeSuggestionBackendType,
        IConfiguration configuration,
        string programmingLanguage,
        string additionalSystemInstruction,
        bool defaultToNoneIdConfigError = false)
    {
        ICodeSuggestion codeSuggestion;
        try
        {
            codeSuggestion = codeSuggestionBackendType switch
            {
                CodeSuggestionBackendTypeEnum.OpenAI => OpenAICodeSuggestion.CreateOpenAICodeSuggestion(configuration, programmingLanguage, additionalSystemInstruction),
                CodeSuggestionBackendTypeEnum.OpenAISelfHostedCodeLlama => OpenAICodeSuggestion.CreateOpenAICodeSuggestionForCodeLlama(configuration, programmingLanguage, additionalSystemInstruction),
                CodeSuggestionBackendTypeEnum.CustomEndpoint => new CustomAIEndpointCodeSuggestion(configuration, programmingLanguage),
                CodeSuggestionBackendTypeEnum.CustomEndpoint2 => OpenAICodeSuggestion.CreateCustomAIEndpointCodeSuggestion(configuration, programmingLanguage, additionalSystemInstruction),
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
