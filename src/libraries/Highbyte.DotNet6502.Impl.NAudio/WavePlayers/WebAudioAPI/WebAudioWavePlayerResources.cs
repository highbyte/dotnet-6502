using System.Runtime.Versioning;

namespace Highbyte.DotNet6502.Impl.NAudio.WavePlayers.WebAudioAPI;

[SupportedOSPlatform("browser")]

/// <summary>
/// Provides access to embedded JavaScript resources for the WebAudioWavePlayer.
/// The JavaScript module is embedded as a resource in the assembly and can be retrieved to be loaded in .NET WebAssembly application for JS interop.
/// </summary>
public static class WebAudioWavePlayerResources
{
    public static string GetJavaScriptModule()
    {
        var assembly = typeof(WebAudioWavePlayerResources).Assembly;
        var resourceName = "Highbyte.DotNet6502.Impl.NAudio.WavePlayers.WebAudioAPI.WebAudioWavePlayer.js";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Gets the JavaScript module as a data URI that can be used to import the module in a .NET WebAssembly application.
    /// Example:
    /// 
    /// var jsModuleUri = WebAudioWavePlayerResources.GetJavaScriptModuleDataUri();
    /// await JSHost.ImportAsync("WebAudioWavePlayer", jsModuleUri);
    /// </summary>
    /// <returns></returns>
    public static string GetJavaScriptModuleDataUri()
    {
        var jsContent = GetJavaScriptModule();
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(jsContent));
        return $"data:text/javascript;base64,{base64}";
    }
}
