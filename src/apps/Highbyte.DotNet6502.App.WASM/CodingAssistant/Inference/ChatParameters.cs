// Based on https://github.com/dotnet/smartcomponents

namespace Highbyte.DotNet6502.App.WASM.CodingAssistant.Inference;

public class ChatParameters
{
    public IList<ChatMessage>? Messages { get; set; }
    public float? Temperature { get; set; }
    public float? TopP { get; set; }
    public int? MaxTokens { get; set; }
    public float? FrequencyPenalty { get; set; }
    public float? PresencePenalty { get; set; }
    public IList<string>? StopSequences { get; set; }
}

public class ChatMessage(ChatMessageRole role, string text)
{
    public ChatMessageRole Role => role;
    public string Text => text;
}

public enum ChatMessageRole
{
    System,
    User,
    Assistant,
}
