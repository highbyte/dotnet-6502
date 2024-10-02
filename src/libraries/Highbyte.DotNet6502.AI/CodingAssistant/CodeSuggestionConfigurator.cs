using Microsoft.Extensions.Configuration;

namespace Highbyte.DotNet6502.AI.CodingAssistant;

public static class CodeSuggestionConfigurator
{
    public static ICodeSuggestion CreateCodeSuggestion(
        CodeSuggestionBackendTypeEnum codeSuggestionBackendType,
        IConfiguration configuration,
        string programmingLanguage,
        bool defaultToNoneIdConfigError = false)
    {
        ICodeSuggestion codeSuggestion;
        try
        {
            codeSuggestion = codeSuggestionBackendType switch
            {
                CodeSuggestionBackendTypeEnum.OpenAI => new OpenAICodeSuggestion(configuration, programmingLanguage),
                CodeSuggestionBackendTypeEnum.CustomEndpoint => new CustomAIEndpointCodeSuggestion(configuration, programmingLanguage),
                CodeSuggestionBackendTypeEnum.SelfHostedOpenAICompatible => new OpenAICodeSuggestion(configuration, programmingLanguage),
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
