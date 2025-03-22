// Based on https://github.com/dotnet/smartcomponents
// and modified for Microsoft.Extensions.AI
using System.Text;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference;

public class CodeCompletionInference(IChatClient chatClient)
{
    private readonly IChatClient _chatClient = chatClient;

    public virtual ChatOptions BuildChatOptions(CodeCompletionConfig config)
    {
        return new ChatOptions
        {
            Temperature = 0,
            TopP = 1,
            MaxOutputTokens = 400,
            StopSequences = config.StopSequences,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
        };
    }

    public virtual IList<Microsoft.Extensions.AI.ChatMessage> BuildChatMessages(CodeCompletionConfig config, string textBefore, string textAfter)
    {
        // Add system instruction
        var systemMessageBuilder = new StringBuilder();
        systemMessageBuilder.Append(config.SystemInstruction);
        List<Microsoft.Extensions.AI.ChatMessage> messages =
        [
            new(ChatRole.System, systemMessageBuilder.ToString()),
        ];

        // Add examples
        if (config.Examples != null)
            messages.AddRange(config.Examples);

        // Add user-entered text
        messages.Add(new(ChatRole.User, string.Format(config.UserMessageFormat, textBefore, textAfter)));

        return messages;
    }

    public virtual async Task<string> GetInsertionSuggestionAsync(CodeCompletionConfig config, string textBefore, string textAfter)
    {
        var chatOptions = BuildChatOptions(config);
        var chatMessages = BuildChatMessages(config, textBefore, textAfter);
        ChatResponse completionsResponse = await _chatClient.GetResponseAsync(chatMessages, chatOptions);
        var response = completionsResponse.Messages.FirstOrDefault()?.Text ?? string.Empty;

        return config.ParseResponse(response, textBefore, textAfter);

    }
}
