using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// Transfer Accumulator to X.
/// Copies the current contents of the accumulator into the X register and sets the zero and negative flags as appropriate.
/// </summary>
public class TAX : Instruction, IInstructionUsesOnlyRegOrStatus
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;
    public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
    {
        cpu.X = cpu.A;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.X, cpu.ProcessorStatus);            

        return 0;                
    }

    public TAX()
    {
        _opCodes = new List<OpCode>
            {
                new OpCode
                {
                    Code = OpCodeId.TAX,
                    AddressingMode = AddrMode.Implied,
                    Size = 1,
                    MinimumCycles = 2,
                }
        };
    }
}
