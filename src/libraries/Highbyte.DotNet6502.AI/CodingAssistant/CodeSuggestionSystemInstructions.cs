using Highbyte.DotNet6502.AI.CodingAssistant.Inference;

namespace Highbyte.DotNet6502.AI.CodingAssistant;

public static class CodeSuggestionSystemInstructions
{
    public static CodeCompletionConfig GetOpenAICodeCompletionConfig(string programmingLanguage, string addionalSystemInstructions)
    {

        return new CodeCompletionConfig()
        {
            SystemInstruction = @$"You are a Code completion AI assistant who responds exclusively using {programmingLanguage} source code.
Predict what text the user in would insert at the cursor position indicated by ^^^.
Only give predictions for which you have an EXTREMELY high confidence that the user would insert that EXACT text.
Do not make up new information. If you're not sure, just reply with NO_PREDICTION.

RULES:
1. Reply with OK:,then in square brackets [] (without preceeding space) the predicted text, then END_INSERTION, and no other output.
2. If there isn't enough information to predict any words that the user would type next, just reply with the word NO_PREDICTION.
3. NEVER invent new information. If you can't be sure what the user is about to type, ALWAYS stop the prediction with END_INSERTION.",

            Examples = new(),

            UserMessageFormat = "{0}^^^{1}",

            StopSequences = new()
            {
                "END_INSERTION",
                "NEED_INFO",
            },

            ParseResponse = (response, textBefore, textAfter) =>
            {
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
        };
    }

    public static CodeCompletionConfig GetCodeLlamaCodeCompletionConfig(string programmingLanguage, string additionalSystemInstruction)
    {
        return new CodeCompletionConfig()
        {
            SystemInstruction = @$"#You are a {programmingLanguage} code completion assistant.
Don't return any comments.
{additionalSystemInstruction}",

            Examples = new(),

            // CodeLlama-code completion expects the user message to be in the format: <PRE> {before} <SUF> {after} <MID>
            UserMessageFormat = "#Complete this " + $"{programmingLanguage}" + " program: <PRE> {0} <SUF> {1} <MID>",

            StopSequences = new()
            {
                ":",
                "\r",
                "\n",
                "</pre>"
            },

            ParseResponse = (response, textBefore, textAfter) =>
            {
                return response;
            }
        };
    }
}
