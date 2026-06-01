using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// ARR (illegal) — AND immediate then ROR accumulator with non-standard flags.
/// ANDs A with the immediate byte, then rotates A one bit right through carry.
/// Flags differ from a normal ROR: C = bit 6 of result; V = bit 6 XOR bit 5 of result.
/// N and Z are set normally.
/// </summary>
public class ARR : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        cpu.A &= value;

        bool oldCarry = cpu.ProcessorStatus.Carry;
        cpu.A = (byte)((cpu.A >> 1) | (oldCarry ? 0x80 : 0x00));

        // Non-standard flag behaviour
        cpu.ProcessorStatus.Carry    = cpu.A.IsBitSet(6);
        cpu.ProcessorStatus.Overflow = cpu.A.IsBitSet(6) ^ cpu.A.IsBitSet(5);
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, ref cpu.ProcessorStatus);
        return 0;
    }

    public ARR()
    {
        _opCodes = new List<OpCode>
        {
            new OpCode { Code = OpCodeId.ARR_I, AddressingMode = AddrMode.I, Size = 2, MinimumCycles = 2 },
        };
    }
}
