namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// The general, system-agnostic parameters of an automated startup, as supplied via command-line
/// arguments (desktop / headless) or URL query parameters (browser). Consumed by
/// <see cref="AutomatedStartupHandler"/> and passed to <see cref="IAutomatedStartupParticipant"/>.
/// </summary>
/// <remarks>
/// System-specific automation parameters (e.g. C64 BASIC source text) are deliberately not part
/// of this record. See <c>docs/automated-startup-abstraction.md</c>.
/// </remarks>
public sealed record AutomatedStartupRequest(
    string SystemName,
    string? SystemVariant,
    bool AutoStart,
    bool WaitForSystemReady,
    string? LoadPrgPath,
    bool RunLoadedProgram,
    bool EnableExternalDebug)
{
    /// <summary>
    /// System-specific automation parameters that the generic pipeline does not interpret. The
    /// host input adapter routes every non-general named parameter here verbatim; an
    /// <see cref="IAutomatedStartupParticipant"/> reads its own keys (see
    /// <c>docs/automated-startup-seam2.md</c>). Empty by default.
    /// </summary>
    public IReadOnlyDictionary<string, string> ExtraParameters { get; init; }
        = new Dictionary<string, string>();

    /// <summary>
    /// Optional path (or host-resolvable resource id) of an emulator state snapshot to load at
    /// startup. When set, the snapshot's manifest determines the machine, so
    /// <see cref="SystemName"/> is not required — <see cref="AutomatedStartupHandler"/> restores
    /// the system from the snapshot (leaving it paused) before any optional autostart, instead of
    /// the normal select-system / load-PRG flow.
    /// </summary>
    public string? LoadSnapshotPath { get; init; }
}
