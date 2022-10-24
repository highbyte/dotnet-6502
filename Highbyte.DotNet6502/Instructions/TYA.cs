using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Transfer Y to Accumulator.
    /// Copies the current contents of the Y register into the accumulator and sets the zero and negative flags as appropriate.
    /// </summary>
    public class TYA : Instruction, IInstructionUsesOnlyRegOrStatus
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.A = cpu.Y;
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, cpu.ProcessorStatus);
            
            return 0;                
        }

        public TYA()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.TYA,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 2,
                    }
            };
        }
    }
}
