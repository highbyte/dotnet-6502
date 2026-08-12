namespace Highbyte.DotNet6502;

/// <summary>
/// Registry of all CPU model definitions, keyed by their stable model id.
/// Manual list — AOT and trimming safe.
/// </summary>
internal static class CpuModels
{
    public static CpuModelDefinition GetDefinition(string cpuModelId)
        => cpuModelId switch
        {
            CpuModelIds.Nmos6502 => Nmos6502Model.Definition,
            CpuModelIds.Ncr65c02 => Ncr65c02Model.Definition,
            _ => throw new DotNet6502Exception($"Unknown CPU model id '{cpuModelId}'."),
        };
}
