namespace Highbyte.DotNet6502.AI.CodingAssistant;

public interface ICodeSuggestion
{
    bool IsAvailable { get; }

    Task<string> GetInsertionSuggestionAsync(string textBefore, string textAfter);
}
