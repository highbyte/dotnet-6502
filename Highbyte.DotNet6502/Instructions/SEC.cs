namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// Set Carry Flag.
/// Set the carry flag to one.
/// </summary>
public class SEC : Instruction, IInstructionUsesOnlyRegOrStatus
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;
    public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
    {
        // Assume implied mode
        cpu.ProcessorStatus.Carry = true;
                    
        return 0;
    }

    public SEC()
    {
        _opCodes = new List<OpCode>
            {
                new OpCode
                {
                    Code = OpCodeId.SEC,
                    AddressingMode = AddrMode.Implied,
                    Size = 1,
                    MinimumCycles = 2,
                }
        };
    }
}
