// Based on https://github.com/dotnet/smartcomponents
using System.Text;

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference;

public class CodeCompletionInference

{
    public virtual ChatParameters BuildPrompt(CodeCompletionConfig config, string textBefore, string textAfter)
    {
        var systemMessageBuilder = new StringBuilder();
        systemMessageBuilder.Append(@$"You are a Code completion AI assistant who responds exclusively using {config.ProgrammingLanguage} source code.
Predict what text the user in would insert at the cursor position indicated by ^^^.
Only give predictions for which you have an EXTREMELY high confidence that the user would insert that EXACT text.
Do not make up new information. If you're not sure, just reply with NO_PREDICTION.

RULES:
1. Reply with OK:,then in square brackets [] (without preceeding space) the predicted text, then END_INSERTION, and no other output.
2. If there isn't enough information to predict any words that the user would type next, just reply with the word NO_PREDICTION.
3. NEVER invent new information. If you can't be sure what the user is about to type, ALWAYS stop the prediction with END_INSERTION.");


        //if (config.UserPhrases is { Length: > 0 } stockPhrases)
        //{
        //    systemMessageBuilder.Append("\nAlways try to use variations on the following phrases as part of the predictions:\n");
        //    foreach (var phrase in stockPhrases)
        //    {
        //        systemMessageBuilder.AppendFormat("- {0}\n", phrase);
        //    }
        //}

        List<ChatMessage> messages =
        [
            // System instruction
            new(ChatMessageRole.System, systemMessageBuilder.ToString()),
        ];

        // Add examples
        messages.AddRange(config.Examples);

        // Add user-entered text
        messages.Add(new(ChatMessageRole.User, @$"{textBefore}^^^{textAfter}"));

        return new ChatParameters
        {
            Messages = messages,
            Temperature = 0,
            MaxTokens = 400,
            StopSequences = ["END_INSERTION", "NEED_INFO"],
            FrequencyPenalty = 0,
            PresencePenalty = 0,
        };
    }

    public virtual async Task<string> GetInsertionSuggestionAsync(IInferenceBackend inference, CodeCompletionConfig config, string textBefore, string textAfter)
    {
        var chatOptions = BuildPrompt(config, textBefore, textAfter);
        var response = await inference.GetChatResponseAsync(chatOptions);

        if (response.Length > 5 &&
            (response.StartsWith("OK:[", StringComparison.Ordinal)
            || response.StartsWith("OK: [", StringComparison.Ordinal)))
        {
            // Some tested Ollama models respons starts with "OK: [" , some with "OK:[" (even though the prompt doesn't have a space)
            if (response.StartsWith("OK: [", StringComparison.Ordinal))
                response = response.Replace("OK: [", "OK:[");

            // Avoid returning multiple sentences as it's unlikely to avoid inventing some new train of thought.
            var trimAfter = response.IndexOfAny(['.', '?', '!']);
            if (trimAfter > 0 && response.Length > trimAfter + 1 && response[trimAfter + 1] == ' ')
                response = response.Substring(0, trimAfter + 1);

            // Leave it up to the frontend code to decide whether to add a training space
            var trimmedResponse = response.Substring(4).TrimEnd(']', ' ');

            // Don't have a leading space on the suggestion if there's already a space right
            // before the cursor. The language model normally gets this right anyway (distinguishing
            // between starting a new word, vs continuing a partly-typed one) but sometimes it adds
            // an unnecessary extra space.
            if (textBefore.Length > 0 && textBefore[textBefore.Length - 1] == ' ')
                trimmedResponse = trimmedResponse.TrimStart(' ');

            return trimmedResponse;
        }

        return string.Empty;
    }
}
