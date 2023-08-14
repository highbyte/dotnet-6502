namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// Transfer Stack Pointer to X.
/// Copies the current contents of the stack register into the X register and sets the zero and negative flags as appropriate.
/// </summary>
public class TSX : Instruction, IInstructionUsesOnlyRegOrStatus
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;
    public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
    {
        cpu.X = cpu.SP;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.X, cpu.ProcessorStatus);
        
        return 0;                
    }

    public TSX()
    {
        _opCodes = new List<OpCode>
            {
                new OpCode
                {
                    Code = OpCodeId.TSX,
                    AddressingMode = AddrMode.Implied,
                    Size = 1,
                    MinimumCycles = 2,
                }
        };
    }
}
