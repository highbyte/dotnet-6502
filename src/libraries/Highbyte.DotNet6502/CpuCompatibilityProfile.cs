namespace Highbyte.DotNet6502;

/// <summary>
/// Controls which undocumented NMOS 6502 opcodes are exposed through a CPU's instruction list.
/// Higher values include all opcodes from lower values.
/// </summary>
public enum CpuCompatibilityProfile
{
    OfficialOnly = 0,
    StableUnofficial = 1,
    ExperimentalUnofficial = 2,
    FullUnofficial = 3,
}
