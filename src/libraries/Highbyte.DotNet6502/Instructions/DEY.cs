namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// Decrement Y Register.
/// Subtracts one from the Y register setting the zero and negative flags as appropriate.
/// </summary>
public class DEY : Instruction, IInstructionUsesOnlyRegOrStatus
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;
    public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
    {
        // Assume implied mode
        cpu.Y--;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.Y, cpu.ProcessorStatus);

        return 0;
    }
    public DEY()
    {
        _opCodes = new List<OpCode>
            {
                new OpCode
                {
                    Code = OpCodeId.DEY,
                    AddressingMode = AddrMode.Implied,
                    Size = 1,
                    MinimumCycles = 2,
                }
        };
    }
}
