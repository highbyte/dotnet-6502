using System.Runtime.InteropServices;

namespace Highbyte.DotNet6502.Tests.Helpers;

/// <summary>
/// A <see cref="FactAttribute"/> that skips the test on non-Windows platforms.
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Skip = "Test requires Windows (uses a Windows .exe assembler binary).";
    }
}
