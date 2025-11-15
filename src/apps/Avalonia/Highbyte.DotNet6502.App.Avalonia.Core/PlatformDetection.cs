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

    /// <summary>
    /// Detects if the application is running on a desktop platform (Windows, Linux, macOS)
    /// </summary>
    /// <returns>True if running on desktop, false otherwise</returns>
    internal static bool IsRunningOnDesktop()
    {
        // If running in WebAssembly, it's not desktop
        if (IsRunningInWebAssembly())
            return false;

        try
        {
            // Check for common desktop platforms
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                   RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                   RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        }
        catch
        {
            // If any check fails, assume we're not on desktop to be safe
            return false;
        }
    }
}
