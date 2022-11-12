using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Transfer X to Accumulator.
    /// Copies the current contents of the X register into the accumulator and sets the zero and negative flags as appropriate.
    /// </summary>
    public class TXA : Instruction, IInstructionUsesOnlyRegOrStatus
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.A = cpu.X;
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, cpu.ProcessorStatus);
            
            return 0;                
        }

        public TXA()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.TXA,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 2,
                    }
            };
        }
    }
}
