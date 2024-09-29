namespace Highbyte.DotNet6502.AI.CodingAssistant;

/// <summary>
/// A dummy implementation of <see cref="ICodeSuggestion"/> that does not suggest any code completions.
/// </summary>
public class NoCodeSuggestion : ICodeSuggestion
{
    public bool IsAvailable => false;

    public string? LastError => null;

    public Task CheckAvailability()
    {
        return Task.CompletedTask;
    }

    public virtual Task<string> GetInsertionSuggestionAsync(string textBefore, string textAfter)
    {
        return Task.FromResult(string.Empty);
    }
}
