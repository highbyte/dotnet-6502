namespace Highbyte.DotNet6502.AI.CodingAssistant;

public interface ICodeSuggestion
{
    bool IsAvailable { get; }
    string? LastError { get; }

    Task CheckAvailability();

    Task<string> GetInsertionSuggestionAsync(string textBefore, string textAfter);
}
