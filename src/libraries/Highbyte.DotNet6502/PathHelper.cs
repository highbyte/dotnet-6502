using System.Runtime.InteropServices;

namespace Highbyte.DotNet6502;

public static class PathHelper
{
    private static readonly StringComparison s_stringComparison = StringComparison.InvariantCultureIgnoreCase;

    public static string ExpandOSEnvironmentVariables(string path)
    {
        var result = path;
        result = ReplaceOSSpecificVariablesForLinuxAndMac(result);
        result = ReplaceDirectorySeparator(result);
        result = Environment.ExpandEnvironmentVariables(result);
        return result;
    }

    private static string ReplaceDirectorySeparator(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            path = path.Replace(@"\", "/", s_stringComparison);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            path = path.Replace("/", @"\", s_stringComparison);
        }
        return path;
    }

    private static string ReplaceOSSpecificVariablesForLinuxAndMac(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            path = path.Replace("%USERPROFILE%", "%HOME%", s_stringComparison);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            path = path.Replace("%HOME%", "%USERPROFILE%", s_stringComparison);
            // TODO: More Linux/Mac specific that need replacements on Windows
        }
        return path;
    }
}
