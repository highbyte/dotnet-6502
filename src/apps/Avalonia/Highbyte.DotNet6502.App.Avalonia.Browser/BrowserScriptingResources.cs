using System.Runtime.Versioning;
using System.Text;

[SupportedOSPlatform("browser")]
internal static class BrowserScriptingResources
{
    public static string GetJavaScriptModuleDataUri()
    {
        var assembly = typeof(BrowserScriptingResources).Assembly;
        var resourceName = "Highbyte.DotNet6502.App.Avalonia.Browser.BrowserScripting.js";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(reader.ReadToEnd()));
        return $"data:text/javascript;base64,{base64}";
    }
}
