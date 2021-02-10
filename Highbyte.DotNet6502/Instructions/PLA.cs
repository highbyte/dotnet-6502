using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Pull Accumulator.
    ///Pulls an 8 bit value from the stack and into the accumulator. The zero and negative flags are set as appropriate.
    /// </summary>
    public class PLA : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.A = cpu.PopByteFromStack(mem);
            // Consume two extra cycles to change SP? Why one more than PHA?
            cpu.ExecState.CyclesConsumed += 2;
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, cpu.ProcessorStatus);

            return true;
        }
        
        public PLA()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.PLA,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 4,
                    }
            };
        }
    }
}
