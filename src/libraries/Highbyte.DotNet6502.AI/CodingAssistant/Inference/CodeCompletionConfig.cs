// Based on https://github.com/dotnet/smartcomponents

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference;

public class CodeCompletionConfig
{
    public string ProgrammingLanguage { get; set; } = "Basic";
    public string SystemInstruction { get; set; } = "Insert the code that appears at the location indicated by ^^^";
    public List<ChatMessage> Examples { get; set; } = new();
    public string UserMessageFormat { get; set; } = "{0}^^^{1}";
    public List<string> StopSequences { get; set; } = new();

    public Func<string, string, string, string> ParseResponse { get; set; } = (response, textBefore, textAfter) => response;

    //public string? Parameters { get; set; }
    //public string? UserRole { get; set; }
    //public string[]? UserPhrases { get; set; }
}
