// Based on https://github.com/dotnet/smartcomponents

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference;

public interface IInferenceBackend
{
    Task<string> GetChatResponseAsync(ChatParameters options);
}
