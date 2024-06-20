namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// Transfer Accumulator to Y.
/// Copies the current contents of the accumulator into the Y register and sets the zero and negative flags as appropriate.
/// </summary>
public class TAY : Instruction, IInstructionUsesOnlyRegOrStatus
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;
    public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
    {
        cpu.Y = cpu.A;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.Y, ref cpu.ProcessorStatus);
        
        return 0;                
    }

    public TAY()
    {
        _opCodes = new List<OpCode>
            {
                new OpCode
                {
                    Code = OpCodeId.TAY,
                    AddressingMode = AddrMode.Implied,
                    Size = 1,
                    MinimumCycles = 2,
                }
        };
    }
}
