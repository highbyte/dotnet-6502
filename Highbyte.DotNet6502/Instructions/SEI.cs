using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// Set Interrupt Disable.
/// Set the interrupt disable flag to one.
/// </summary>
public class SEI : Instruction, IInstructionUsesOnlyRegOrStatus
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;
    public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
    {
        // Assume implied mode
        cpu.ProcessorStatus.InterruptDisable = true;
                    
        return 0;
    }

    public SEI()
    {
        _opCodes = new List<OpCode>
            {
                new OpCode
                {
                    Code = OpCodeId.SEI,
                    AddressingMode = AddrMode.Implied,
                    Size = 1,
                    MinimumCycles = 2,
                }
        };
    }
}
