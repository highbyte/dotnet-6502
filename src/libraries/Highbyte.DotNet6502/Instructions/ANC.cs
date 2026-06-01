using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// ANC (illegal) — AND immediate, then copy bit 7 of result to Carry.
/// Opcodes 0x0B and 0x2B both perform the same operation.
/// </summary>
public class ANC : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        cpu.A &= value;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, ref cpu.ProcessorStatus);
        cpu.ProcessorStatus.Carry = cpu.A.IsBitSet(7);
        return 0;
    }

    public ANC()
    {
        _opCodes = new List<OpCode>
        {
            new OpCode { Code = OpCodeId.ANC_I_0B, AddressingMode = AddrMode.I, Size = 2, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.ANC_I_2B, AddressingMode = AddrMode.I, Size = 2, MinimumCycles = 2 },
        };
    }
}
