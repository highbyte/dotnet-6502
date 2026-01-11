using System.Runtime.Versioning;
using System.Text;

namespace Highbyte.DotNet6502.Impl.Browser.Input;

[SupportedOSPlatform("browser")]

/// <summary>
/// Provides access to embedded JavaScript resources for the BrowserGamepad.
/// The JavaScript module is embedded as a resource in the assembly and can be retrieved 
/// to be loaded in .NET WebAssembly application for JS interop.
/// </summary>
public static class BrowserGamepadResources
{
    /// <summary>
    /// Gets the raw JavaScript module code from the embedded resource.
    /// </summary>
    public static string GetJavaScriptModule()
    {
        var assembly = typeof(BrowserGamepadResources).Assembly;
        var resourceName = "Highbyte.DotNet6502.Impl.Browser.Input.BrowserGamepad.js";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Gets the JavaScript module as a data URI that can be used with JSHost.ImportAsync.
    /// Example:
    /// 
    /// var jsModuleUri = BrowserGamepadResources.GetJavaScriptModuleDataUri();
    /// await JSHost.ImportAsync("BrowserGamepad", jsModuleUri);
    /// </summary>
    public static string GetJavaScriptModuleDataUri()
    {
        var jsContent = GetJavaScriptModule();
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsContent));
        return $"data:text/javascript;base64,{base64}";
    }
}
