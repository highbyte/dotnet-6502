namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// Rotate Left.
/// Move each of the bits in either A or M one place to the left.
/// Bit 0 is filled with the current value of the carry flag whilst the old 
/// bit 7 becomes the new carry flag value.
/// </summary>
public class ROL : Instruction, IInstructionUsesAddress, IInstructionUsesOnlyRegOrStatus
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithWord(CPU cpu, Memory mem, ushort address, AddrModeCalcResult addrModeCalcResult)
    {
        var tempValue = cpu.FetchByte(mem, address);
        tempValue = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(tempValue, cpu.ProcessorStatus);
        cpu.StoreByte(tempValue, mem, address);

        return 0;
    }        

    public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
    {
        // Assume Accumulator mode
        cpu.A = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(cpu.A, cpu.ProcessorStatus);

        return 0;
    }  

    public ROL()
    {
        _opCodes = new List<OpCode>
            {
                new OpCode
                {
                    Code = OpCodeId.ROL_ACC,
                    AddressingMode = AddrMode.Accumulator,
                    Size = 1,
                    MinimumCycles = 2,
                },
                new OpCode
                {
                    Code = OpCodeId.ROL_ZP,
                    AddressingMode = AddrMode.ZP,
                    Size = 2,
                    MinimumCycles = 5,
                },
                new OpCode
                {
                    Code = OpCodeId.ROL_ZP_X,
                    AddressingMode = AddrMode.ZP_X,
                    Size = 2,
                    MinimumCycles = 6,
                },
                new OpCode
                {
                    Code = OpCodeId.ROL_ABS,
                    AddressingMode = AddrMode.ABS,
                    Size = 3,
                    MinimumCycles = 6,
                },
                new OpCode
                {
                    Code = OpCodeId.ROL_ABS_X,
                    AddressingMode = AddrMode.ABS_X,
                    Size = 3,
                    MinimumCycles = 7,
                },
        };
    }
}
