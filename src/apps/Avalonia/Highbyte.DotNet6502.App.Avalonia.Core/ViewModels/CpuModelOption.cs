using System.Collections.Generic;
using System.Linq;
using Highbyte.DotNet6502;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

/// <summary>
/// Dropdown option for selecting a CPU model in a system config dialog. Model ids and
/// display names come from <see cref="CpuModelInfo"/>; each system dialog picks the
/// subset of models that makes sense for its machine via <see cref="ForModelIds"/>.
/// </summary>
public record CpuModelOption(string ModelId, string DisplayName, string HelpText)
{
    public static IReadOnlyList<CpuModelOption> All { get; } =
        CpuModelInfo.AllModelIds.Select(modelId => new CpuModelOption(
            modelId,
            CpuModelInfo.GetDisplayName(modelId),
            GetHelpText(modelId))).ToList();

    public static CpuModelOption FromModelId(string modelId)
        => All.Single(option => option.ModelId == modelId);

    /// <summary>The options for a system-specific allow-list of model ids, in the given order.</summary>
    public static IReadOnlyList<CpuModelOption> ForModelIds(params string[] modelIds)
        => modelIds.Select(FromModelId).ToList();

    private static string GetHelpText(string modelId) => modelId switch
    {
        CpuModelIds.Nmos6502 => "The classic NMOS 6502, including profile-selectable undocumented opcodes.",
        CpuModelIds.Mos6510 => "NMOS 6502 core with the 6510's on-chip I/O port at $00/$01 (the C64's CPU).",
        CpuModelIds.Ncr65c02 => "CMOS 65C02 with new and redefined instructions. Supports only the official-only compatibility profile.",
        _ => string.Empty,
    };
}
