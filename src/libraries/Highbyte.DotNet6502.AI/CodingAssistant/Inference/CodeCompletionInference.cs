// Based on https://github.com/dotnet/smartcomponents
using System.Text;

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference;

public class CodeCompletionInference

{
    public virtual ChatParameters BuildPrompt(CodeCompletionConfig config, string textBefore, string textAfter)
    {
        var systemMessageBuilder = new StringBuilder();
        systemMessageBuilder.Append(config.SystemInstruction);

        List<ChatMessage> messages =
        [
            // System instruction
            new(ChatMessageRole.System, systemMessageBuilder.ToString()),
        ];

        // Add examples
        if (config.Examples != null)
            messages.AddRange(config.Examples);

        // Add user-entered text
        messages.Add(new(ChatMessageRole.User, string.Format(config.UserMessageFormat, textBefore, textAfter)));

        return new ChatParameters
        {
            Messages = messages,
            Temperature = 0,
            MaxTokens = 400,
            StopSequences = config.StopSequences,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
        };
    }

    public virtual async Task<string> GetInsertionSuggestionAsync(IInferenceBackend inference, CodeCompletionConfig config, string textBefore, string textAfter)
    {
        var chatOptions = BuildPrompt(config, textBefore, textAfter);
        var response = await inference.GetChatResponseAsync(chatOptions);

        return config.ParseResponse(response, textBefore, textAfter);

    }
}
