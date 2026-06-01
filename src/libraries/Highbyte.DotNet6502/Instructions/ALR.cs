namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// ALR (illegal, also ASR) — AND immediate then LSR accumulator.
/// ANDs A with the immediate byte, then shifts A one bit right
/// (bit 7 = 0, old bit 0 → C), setting N, Z, and C flags.
/// </summary>
public class ALR : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        cpu.A &= value;
        cpu.A = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(cpu.A, ref cpu.ProcessorStatus);
        return 0;
    }

    public ALR()
    {
        _opCodes = new List<OpCode>
        {
            new OpCode { Code = OpCodeId.ALR_I, AddressingMode = AddrMode.I, Size = 2, MinimumCycles = 2 },
        };
    }
}
