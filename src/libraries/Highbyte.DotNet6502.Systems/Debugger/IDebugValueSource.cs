namespace Highbyte.DotNet6502.Systems.Debugger;

/// <summary>
/// Named values a system exposes to debuggers beyond the CPU's registers and flags: they can be
/// used in breakpoint conditions and shown next to the registers. A C64 exposes its VIC-II raster
/// position this way.
/// </summary>
public interface IDebugValueSource
{
    /// <summary>The heading the values are shown under, such as "VIC-II".</summary>
    string DebugValueGroupName { get; }

    /// <summary>The values, in display order. Names are upper-case identifiers.</summary>
    IReadOnlyList<DebugValueInfo> DebugValues { get; }

    /// <summary>
    /// Reads the value named <paramref name="name"/> (case-insensitive) as it is at the current
    /// instruction boundary. Returns false for a name the system does not expose.
    /// </summary>
    bool TryGetDebugValue(string name, out long value);

    /// <summary>
    /// The run-until targets the system offers by name, with their arguments described: a C64
    /// offers "raster" (a line, and optionally a cycle). Empty when it offers none.
    /// </summary>
    IReadOnlyList<DebugValueInfo> RunUntilTargets { get; }

    /// <summary>
    /// Builds the run-until condition for <paramref name="target"/> (case-insensitive) with the
    /// given arguments, in breakpoint-condition syntax. Returns false when the target is not one
    /// the system offers; throws <see cref="ArgumentException"/> when the arguments are wrong.
    /// </summary>
    bool TryBuildRunUntilCondition(string target, IReadOnlyList<string> arguments, out string condition);
}

/// <summary>A debug value's name and a one-line description of what it holds.</summary>
public readonly record struct DebugValueInfo(string Name, string Description);

public static class DebugValueSourceExtensions
{
    /// <summary>
    /// The source's values on one line, "VIC-II: RASTER=51 CYCLE=12 FRAMECYCLE=3224 FRAME=7" —
    /// the form the monitor's 'r' command and the hosts' status lines share. Null when the source
    /// has no values.
    /// </summary>
    public static string? FormatDebugValuesLine(this IDebugValueSource source)
    {
        if (source.DebugValues.Count == 0)
            return null;
        var sb = new System.Text.StringBuilder(source.DebugValueGroupName).Append(':');
        foreach (var info in source.DebugValues)
        {
            if (source.TryGetDebugValue(info.Name, out var value))
                sb.Append(' ').Append(info.Name).Append('=').Append(value);
        }
        return sb.ToString();
    }
}
