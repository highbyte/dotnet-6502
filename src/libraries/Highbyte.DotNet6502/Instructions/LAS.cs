namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// LAS (illegal, also LAR) — A = X = SP = memory AND SP.
/// Reads a byte from memory, ANDs it with the stack pointer, and stores
/// the result in A, X, and SP. Sets N and Z flags.
/// </summary>
public class LAS : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        byte result = (byte)(value & cpu.SP);
        cpu.A  = result;
        cpu.X  = result;
        cpu.SP = result;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(result, ref cpu.ProcessorStatus);

        return InstructionExtraCyclesCalculator.CalculateExtraCycles(
            addrModeCalcResult.OpCode.AddressingMode,
            addrModeCalcResult.AddressCalculationCrossedPageBoundary);
    }

    public LAS()
    {
        _opCodes = new List<OpCode>
        {
            new OpCode { Code = OpCodeId.LAS_ABS_Y, AddressingMode = AddrMode.ABS_Y, Size = 3, MinimumCycles = 4 }, // +1 page cross
        };
    }
}
