using Blazored.LocalStorage;

namespace Highbyte.DotNet6502.App.WASM.Skia;

public class BrowserContext
{
    public Uri Uri { get; set; }
    public HttpClient HttpClient { get; set; }
    public ILocalStorageService LocalStorage { get; set; }
}
