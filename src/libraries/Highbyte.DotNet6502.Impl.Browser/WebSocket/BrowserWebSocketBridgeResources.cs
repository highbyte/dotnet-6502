using System.Runtime.Versioning;
using System.Text;

namespace Highbyte.DotNet6502.Impl.Browser.WebSocket;

[SupportedOSPlatform("browser")]
public static class BrowserWebSocketBridgeResources
{
    public static string GetJavaScriptModule()
    {
        var assembly = typeof(BrowserWebSocketBridgeResources).Assembly;
        const string resourceName = "Highbyte.DotNet6502.Impl.Browser.WebSocket.BrowserWebSocketBridge.js";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string GetJavaScriptModuleDataUri()
    {
        var jsContent = GetJavaScriptModule();
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsContent));
        return $"data:text/javascript;base64,{base64}";
    }
}
