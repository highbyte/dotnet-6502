// Based on https://github.com/dotnet/smartcomponents

namespace Highbyte.DotNet6502.App.SadConsole.CodingAssistant.Inference;

public struct CodeCompletionConfig
{
    public string? Parameters { get; set; }
    public string? UserRole { get; set; }
    //public string[]? UserPhrases { get; set; }
}
