namespace Highbyte.DotNet6502;

/// <summary>
/// Model-correct public metadata for one opcode byte on a specific CPU (model +
/// compatibility profile), obtained via <see cref="CPU.GetOpCodeInfo"/>. Projected from
/// the CPU model's internal descriptor table, so it always matches what the executor
/// actually runs — including bytes that mean different instructions on different models
/// (e.g. $9C is the undocumented SHY abs,X on NMOS but STZ abs on a 65C02).
/// </summary>
public readonly record struct OpCodeInfo
{
    /// <summary>Raw opcode byte.</summary>
    public required byte Code { get; init; }

    /// <summary>Model-relative instruction name (e.g. "LDA", "STZ").</summary>
    public required string Mnemonic { get; init; }

    public required AddrMode AddressingMode { get; init; }

    /// <summary>Instruction size in bytes, including the opcode byte.</summary>
    public required byte Size { get; init; }

    /// <summary>Cycles consumed when no extra (page-cross/branch) cycles apply.</summary>
    public required ulong MinimumCycles { get; init; }

    /// <summary>True for officially documented opcodes; false for undocumented ones.</summary>
    public required bool Documented { get; init; }
}
