using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Bit Test.
    /// This instructions is used to test if one or more bits are set in a target memory location.
    /// The mask pattern in A is ANDed with the value in memory to set or clear the zero flag, 
    /// but the result is not kept. Bits 7 and 6 of the value from memory are copied into the N and V flags.
    /// </summary>
    public class BIT : Instruction, IInstructionUsesByte
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        public InstructionLogicResult ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
        {
            BinaryArithmeticHelpers.PerformBITAndSetStatusRegisters(cpu.A, value, cpu.ProcessorStatus);

            return InstructionLogicResult.WithNoExtraCycles();
        }  

        public BIT()
        {
            _opCodes = new List<OpCode>
            {
                new OpCode
                {

                    Code = OpCodeId.BIT_ZP,
                    AddressingMode = AddrMode.ZP,
                    Size = 2,
                    MinimumCycles = 3,
                },
                new OpCode
                {

                    Code = OpCodeId.BIT_ABS,
                    AddressingMode = AddrMode.ABS,
                    Size = 3,
                    MinimumCycles = 4,
                },                
            };
        }
    }
}
