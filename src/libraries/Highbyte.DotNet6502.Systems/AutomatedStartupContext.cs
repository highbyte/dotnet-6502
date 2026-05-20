namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Optional host-supplied capabilities passed to an <see cref="IAutomatedStartupParticipant"/>
/// during the automated-startup lifecycle. Lets a participant use host facilities without
/// depending on the host. See <c>docs/automated-startup-seam2.md</c>.
/// </summary>
public sealed class AutomatedStartupContext
{
    /// <summary>
    /// Fetches a text resource by URL or path. Host-supplied (e.g. browser: HTTP from the app
    /// origin). <see langword="null"/> when the host offers none — a participant must then skip
    /// any parameter that needs it.
    /// </summary>
    public Func<string, Task<string>>? FetchTextResource { get; init; }
}
