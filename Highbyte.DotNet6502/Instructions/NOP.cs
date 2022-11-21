using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// No Operation.
/// The NOP instruction causes no changes to the processor other than the normal incrementing of the program counter to the next instruction.
/// </summary>
public class NOP : Instruction, IInstructionUsesOnlyRegOrStatus
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;
    public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
    {
        // Do nothing!
        return 0;
    }

    public NOP()
    {
        _opCodes = new List<OpCode>
            {
                new OpCode
                {
                    Code = OpCodeId.NOP,
                    AddressingMode = AddrMode.Implied,
                    Size = 1,
                    MinimumCycles = 2,
                }
        };
    }
}
