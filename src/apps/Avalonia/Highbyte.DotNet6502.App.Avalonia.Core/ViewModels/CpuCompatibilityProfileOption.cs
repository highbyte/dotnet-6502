using System.Collections.Generic;
using System.Linq;
using Highbyte.DotNet6502;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public record CpuCompatibilityProfileOption(CpuCompatibilityProfile Profile, string DisplayName, string HelpText)
{
    public static IReadOnlyList<CpuCompatibilityProfileOption> All { get; } = new[]
    {
        new CpuCompatibilityProfileOption(
            CpuCompatibilityProfile.OfficialOnly,
            "Official only",
            "Only documented MOS 6502 opcodes are available."),
        new CpuCompatibilityProfileOption(
            CpuCompatibilityProfile.StableUnofficial,
            "Stable unofficial",
            "Also enables the predictable NMOS unofficial opcodes commonly used on real 6502/6510 hardware."),
        new CpuCompatibilityProfileOption(
            CpuCompatibilityProfile.ExperimentalUnofficial,
            "Experimental unofficial",
            "Also enables the remaining executable undocumented opcodes such as ARR and LAS for targeted compatibility testing."),
        new CpuCompatibilityProfileOption(
            CpuCompatibilityProfile.FullUnofficial,
            "Full unofficial",
            "Also enables halt-style unofficial opcodes such as JAM/KIL that can intentionally stop the CPU."),
    };

    public static CpuCompatibilityProfileOption FromProfile(CpuCompatibilityProfile profile)
        => All.Single(option => option.Profile == profile);
}
