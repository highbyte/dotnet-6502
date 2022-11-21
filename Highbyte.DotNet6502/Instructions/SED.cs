using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// Set Decimal Flag.
/// Set the decimal mode flag to one.
/// </summary>
public class SED : Instruction, IInstructionUsesOnlyRegOrStatus
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;
    public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
    {
        // Assume implied mode
        cpu.ProcessorStatus.Decimal = true;
                    
        return 0;
    }

    public SED()
    {
        _opCodes = new List<OpCode>
            {
                new OpCode
                {
                    Code = OpCodeId.SED,
                    AddressingMode = AddrMode.Implied,
                    Size = 1,
                    MinimumCycles = 2,
                }
        };
    }
}
