namespace Highbyte.DotNet6502;

/// <summary>
/// Public read-only information about the available CPU models, for system
/// configuration and UIs (model pickers, validation). The model definitions
/// themselves are internal.
/// </summary>
public static class CpuModelInfo
{
    /// <summary>All selectable CPU model ids, in presentation order.</summary>
    public static IReadOnlyList<string> AllModelIds { get; } = new[]
    {
        CpuModelIds.Nmos6502,
        CpuModelIds.Mos6510,
        CpuModelIds.Ncr65c02,
    };

    /// <summary>Human-readable name for a model id (e.g. "NMOS 6502", "NCR 65C02").</summary>
    public static string GetDisplayName(string cpuModelId)
        => CpuModels.GetDefinition(cpuModelId).DisplayName;

    /// <summary>True if the model id is a known model.</summary>
    public static bool IsKnownModelId(string cpuModelId)
        => AllModelIds.Contains(cpuModelId);

    /// <summary>
    /// The compatibility profiles a model supports. Profiles control undocumented-NMOS-
    /// opcode exposure, so NMOS models support all of them while the 65C02 (every byte
    /// defined) supports only <see cref="CpuCompatibilityProfile.OfficialOnly"/>.
    /// </summary>
    public static IReadOnlyList<CpuCompatibilityProfile> GetSupportedProfiles(string cpuModelId)
        => CpuModels.GetDefinition(cpuModelId).SupportedProfiles;

    public static bool IsProfileSupported(string cpuModelId, CpuCompatibilityProfile compatibilityProfile)
        => IsKnownModelId(cpuModelId) && GetSupportedProfiles(cpuModelId).Contains(compatibilityProfile);
}
