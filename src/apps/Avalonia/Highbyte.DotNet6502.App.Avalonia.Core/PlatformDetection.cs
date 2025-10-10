using System.Runtime.InteropServices;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

internal static class PlatformDetection
{
    /// <summary>
    /// Detects if the application is running in a WebAssembly environment
    /// </summary>
    /// <returns>True if running in WebAssembly, false otherwise</returns>
    internal static bool IsRunningInWebAssembly()
    {
        try
        {
            // Primary check: WebAssembly runtime characteristics
            // In WebAssembly, RuntimeInformation.IsOSPlatform will return true for Browser
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER")))
                return true;

            // Secondary check: WebAssembly architecture
            if (RuntimeInformation.OSArchitecture == Architecture.Wasm)
                return true;

            // If neither of the reliable checks match, we're not in WebAssembly
            return false;
        }
        catch
        {
            // If any check fails, assume we're not in WebAssembly to be safe
            return false;
        }
    }
}
