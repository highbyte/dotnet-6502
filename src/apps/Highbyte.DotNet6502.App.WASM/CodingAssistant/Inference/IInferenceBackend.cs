// Based on https://github.com/dotnet/smartcomponents

namespace Highbyte.DotNet6502.App.WASM.CodingAssistant.Inference;

public interface IInferenceBackend
{
    Task<string> GetChatResponseAsync(ChatParameters options);
}
