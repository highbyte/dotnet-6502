using Blazored.LocalStorage;

namespace Highbyte.DotNet6502.App.WASM.Emulator;

public class BrowserContext
{
    public required Uri Uri { get; set; }
    public required HttpClient HttpClient { get; set; }
    public required ILocalStorageService LocalStorage { get; set; }
}
