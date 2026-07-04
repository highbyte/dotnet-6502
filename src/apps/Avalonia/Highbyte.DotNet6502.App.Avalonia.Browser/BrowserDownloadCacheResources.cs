using System.Runtime.Versioning;
using System.Text;

namespace Highbyte.DotNet6502.App.Avalonia.Browser;

[SupportedOSPlatform("browser")]
internal static class BrowserDownloadCacheResources
{
    public static string GetJavaScriptModuleDataUri()
    {
        var assembly = typeof(BrowserDownloadCacheResources).Assembly;
        var resourceName = "Highbyte.DotNet6502.App.Avalonia.Browser.BrowserDownloadCache.js";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(reader.ReadToEnd()));
        return $"data:text/javascript;base64,{base64}";
    }
}
