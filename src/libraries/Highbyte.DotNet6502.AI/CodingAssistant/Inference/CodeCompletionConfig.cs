// Based on https://github.com/dotnet/smartcomponents

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference;

public struct CodeCompletionConfig
{
    public string ProgrammingLanguage { get; set; }

    public List<ChatMessage> Examples { get; set; }

    //public string? Parameters { get; set; }
    //public string? UserRole { get; set; }
    //public string[]? UserPhrases { get; set; }
}
