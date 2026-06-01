namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// Illegal NOP variants that read (and discard) a byte from memory.
/// </summary>
public class NOP_Illegal : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        return InstructionExtraCyclesCalculator.CalculateExtraCycles(
            addrModeCalcResult.OpCode.AddressingMode,
            addrModeCalcResult.AddressCalculationCrossedPageBoundary);
    }

    public NOP_Illegal()
    {
        _opCodes = new List<OpCode>
        {
            // Immediate
            new OpCode { Code = OpCodeId.NOP_ILL_IMM_80, AddressingMode = AddrMode.I, Size = 2, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.NOP_ILL_IMM_82, AddressingMode = AddrMode.I, Size = 2, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.NOP_ILL_IMM_89, AddressingMode = AddrMode.I, Size = 2, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.NOP_ILL_IMM_C2, AddressingMode = AddrMode.I, Size = 2, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.NOP_ILL_IMM_E2, AddressingMode = AddrMode.I, Size = 2, MinimumCycles = 2 },

            // Zero page
            new OpCode { Code = OpCodeId.NOP_ILL_ZP_04, AddressingMode = AddrMode.ZP, Size = 2, MinimumCycles = 3 },
            new OpCode { Code = OpCodeId.NOP_ILL_ZP_44, AddressingMode = AddrMode.ZP, Size = 2, MinimumCycles = 3 },
            new OpCode { Code = OpCodeId.NOP_ILL_ZP_64, AddressingMode = AddrMode.ZP, Size = 2, MinimumCycles = 3 },

            // Zero page, X
            new OpCode { Code = OpCodeId.NOP_ILL_ZP_X_14, AddressingMode = AddrMode.ZP_X, Size = 2, MinimumCycles = 4 },
            new OpCode { Code = OpCodeId.NOP_ILL_ZP_X_34, AddressingMode = AddrMode.ZP_X, Size = 2, MinimumCycles = 4 },
            new OpCode { Code = OpCodeId.NOP_ILL_ZP_X_54, AddressingMode = AddrMode.ZP_X, Size = 2, MinimumCycles = 4 },
            new OpCode { Code = OpCodeId.NOP_ILL_ZP_X_74, AddressingMode = AddrMode.ZP_X, Size = 2, MinimumCycles = 4 },
            new OpCode { Code = OpCodeId.NOP_ILL_ZP_X_D4, AddressingMode = AddrMode.ZP_X, Size = 2, MinimumCycles = 4 },
            new OpCode { Code = OpCodeId.NOP_ILL_ZP_X_F4, AddressingMode = AddrMode.ZP_X, Size = 2, MinimumCycles = 4 },

            // Absolute
            new OpCode { Code = OpCodeId.NOP_ILL_ABS, AddressingMode = AddrMode.ABS, Size = 3, MinimumCycles = 4 },

            // Absolute, X (+1 cycle on page cross)
            new OpCode { Code = OpCodeId.NOP_ILL_ABS_X_1C, AddressingMode = AddrMode.ABS_X, Size = 3, MinimumCycles = 4 },
            new OpCode { Code = OpCodeId.NOP_ILL_ABS_X_3C, AddressingMode = AddrMode.ABS_X, Size = 3, MinimumCycles = 4 },
            new OpCode { Code = OpCodeId.NOP_ILL_ABS_X_5C, AddressingMode = AddrMode.ABS_X, Size = 3, MinimumCycles = 4 },
            new OpCode { Code = OpCodeId.NOP_ILL_ABS_X_7C, AddressingMode = AddrMode.ABS_X, Size = 3, MinimumCycles = 4 },
            new OpCode { Code = OpCodeId.NOP_ILL_ABS_X_DC, AddressingMode = AddrMode.ABS_X, Size = 3, MinimumCycles = 4 },
            new OpCode { Code = OpCodeId.NOP_ILL_ABS_X_FC, AddressingMode = AddrMode.ABS_X, Size = 3, MinimumCycles = 4 },
        };
    }
}
