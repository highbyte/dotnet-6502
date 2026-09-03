namespace Highbyte.DotNet6502;

/// <summary>
/// Executes one complete instruction (operand/addressing resolution included) and returns
/// the TOTAL cycles consumed: base cycles plus any page-cross/branch extras.
/// </summary>
internal delegate ulong ExecuteHandler(CPU cpu, Memory mem);

/// <summary>
/// Immutable per-opcode-byte entry in a CPU model's 256-entry dispatch table.
/// One descriptor per byte (not per mnemonic): on a 65C02 the same byte can mean a
/// different instruction than on the NMOS 6502, so identity is (model, byte).
/// The metadata properties serve tooling (disassembler/monitor); <see cref="Execute"/>
/// is the hot path — one array index + one delegate call per instruction.
/// </summary>
internal sealed class OpCodeDescriptor
{
    /// <summary>Raw opcode byte — also the table index.</summary>
    public required byte Code { get; init; }

    /// <summary>Model-relative instruction name (e.g. $9C is SHY on NMOS, STZ on 65C02).</summary>
    public required string Mnemonic { get; init; }

    public required AddrMode Addressing { get; init; }

    /// <summary>Instruction size in bytes, including the opcode byte.</summary>
    public required byte Size { get; init; }

    /// <summary>Cycles consumed when no extra (page-cross/branch) cycles apply.</summary>
    public required ulong BaseCycles { get; init; }

    /// <summary>True for officially documented opcodes; false for undocumented ones.</summary>
    public required bool Documented { get; init; }

    /// <summary>The complete instruction execution, addressing included.</summary>
    public required ExecuteHandler Execute { get; init; }

    /// <summary>
    /// True for instructions that change the I flag in their last cycle, after the CPU has polled
    /// its interrupt lines (CLI, SEI, PLP): the interrupt decision at their boundary uses the flag
    /// as it was before the instruction. RTI changes the flag before the poll and is not marked.
    /// </summary>
    public bool ChangesInterruptDisableAfterPoll { get; init; }
}
