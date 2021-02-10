using System;

namespace Highbyte.DotNet6502
{
    /// <summary>
    /// Executes a CPU instruction
    /// </summary>
    public class InstructionExecutor
    {
        public InstructionExecutor()
        {
        }

        public void Execute(CPU cpu, Memory mem)
        {
            byte opCode = cpu.FetchInstruction(mem);
            Execute(cpu, mem, opCode);
        }

        /// <summary>
        /// Executes an instruction.
        /// Returns true if instruction was handled, false is instruction is unknown.
        /// </summary>
        /// <param name="cpu"></param>
        /// <param name="mem"></param>
        /// <param name="opCode"></param>
        /// <returns></returns>
        public bool Execute(CPU cpu, Memory mem, byte opCode)
        {
            var opCodeObject = cpu.InstructionList.GetOpCode(opCode);
            if(opCodeObject == null)
                return false;
            var instruction = cpu.InstructionList.GetInstruction(opCodeObject);
            if(instruction == null)
                return false;

            AddrModeCalcResult addrModeCalcResult;
            switch(opCodeObject.AddressingMode)
            {
                case AddrMode.I:
                    addrModeCalcResult = GetImmediateModeValue(cpu, mem);
                    break;
                case AddrMode.ZP:
                    addrModeCalcResult = GetZeroPageModeAddress(cpu, mem);
                    break;
                case AddrMode.ZP_X:
                    addrModeCalcResult = GetZeroPageXModeAddress(cpu, mem);
                    break;
                case AddrMode.ZP_Y:
                    addrModeCalcResult = GetZeroPageYModeAddress(cpu, mem);
                    break;
                case AddrMode.ABS:
                    addrModeCalcResult = GetAbsModeAddress(cpu, mem);
                    break;
                case AddrMode.ABS_X:
                    addrModeCalcResult = GetAbsXModeAddress(cpu, mem);
                    break;
                case AddrMode.ABS_Y:
                    addrModeCalcResult = GetAbsYModeAddress(cpu, mem);
                    break;
                case AddrMode.IX_IND:
                    addrModeCalcResult = GetIndexedIndirectModeAddress(cpu, mem);
                    break;  
                case AddrMode.IND_IX:
                    addrModeCalcResult = GetIndirectIndexedModeAddress(cpu, mem);
                    break;
                case AddrMode.Indirect:
                    addrModeCalcResult = GetIndirectModeAddress(cpu, mem);
                    break;
                case AddrMode.Relative:
                    addrModeCalcResult = GetRelativeModeAddress(cpu, mem);
                    break;
                case AddrMode.Accumulator:
                    // This mode has no value or address
                    addrModeCalcResult = new AddrModeCalcResult();
                    break;
                case AddrMode.Implied:
                    // This mode has no value or address
                    addrModeCalcResult = new AddrModeCalcResult();
                    break;

                 default:
                    return false;
            }
            if(addrModeCalcResult==null)
                throw new DotNet6502Exception("Bug detected. Variable addrModeCalcResult expected to be set.");

            // TODO: Ugly setting OpCode here
            addrModeCalcResult.OpCode = opCodeObject; 
            return instruction.Execute(cpu, mem, addrModeCalcResult);
        }

        public AddrModeCalcResult GetImmediateModeValue(CPU cpu, Memory mem)
        {
            return new AddrModeCalcResult
            {
                InsValue = cpu.FetchOperand(mem)
            };
        }
        public AddrModeCalcResult GetZeroPageModeAddress(CPU cpu, Memory mem)
        {
            return new AddrModeCalcResult
            {
                InsAddress = cpu.FetchOperand(mem)
            };
        }

        public AddrModeCalcResult GetZeroPageXModeAddress(CPU cpu, Memory mem)
        {
            var operandAddress = cpu.FetchOperand(mem);
            return new AddrModeCalcResult
            {
                InsAddress = cpu.CalcZeroPageAddressX(operandAddress, wrapZeroPage: true)
            };
        }

        public AddrModeCalcResult GetZeroPageYModeAddress(CPU cpu, Memory mem)
        {
            var operandAddress = cpu.FetchOperand(mem);
            return new AddrModeCalcResult
            {
                InsAddress = cpu.CalcZeroPageAddressY(operandAddress, wrapZeroPage: true)
            };
        }

        public AddrModeCalcResult GetAbsModeAddress(CPU cpu, Memory mem)
        {
            return new AddrModeCalcResult
            {
                InsAddress = cpu.FetchOperandWord(mem)
            };
        }

        public AddrModeCalcResult GetAbsXModeAddress(CPU cpu, Memory mem)
        {
            var operandAddress = cpu.FetchOperandWord(mem);
            // Note: CalcFullAddressX will check if adding X to address will cross page boundary. If so, one more cycle is consumed.
            var insAddress = cpu.CalcFullAddressX(operandAddress, out bool didCrossPageBoundary, false);
            return new AddrModeCalcResult
            {
                InsAddress = insAddress,
                AddressCalculationCrossedPageBoundary = didCrossPageBoundary
            };
        }

        public AddrModeCalcResult GetAbsYModeAddress(CPU cpu, Memory mem)
        {
            var operandAddress = cpu.FetchOperandWord(mem);
            // Note: CalcFullAddressY will check if adding Y to address will cross page boundary. If so, one more cycle is consumed.
            var insAddress = cpu.CalcFullAddressY(operandAddress, out bool didCrossPageBoundary, false);
            return new AddrModeCalcResult
            {
                InsAddress = insAddress,
                AddressCalculationCrossedPageBoundary = didCrossPageBoundary                
            };
        }

        public AddrModeCalcResult GetIndexedIndirectModeAddress(CPU cpu, Memory mem)
        {
            var operandAddress = cpu.FetchOperand(mem);
            var zeroPageAddressX = cpu.CalcZeroPageAddressX(operandAddress);
            return new AddrModeCalcResult
            {
                InsAddress = cpu.FetchWord(mem, zeroPageAddressX)
            };
        }

        public AddrModeCalcResult GetIndirectIndexedModeAddress(CPU cpu, Memory mem)
        {
            var operandAddress = cpu.FetchOperand(mem);
            var indirectIndexedAddress = cpu.FetchWord(mem, operandAddress);
            // Note: CalcFullAddressY will check if adding Y to address will cross page boundary. If so, one more cycle is consumed.
            var insAddress = cpu.CalcFullAddressY(indirectIndexedAddress, out bool didCrossPageBoundary, false);
            return new AddrModeCalcResult
            {
                InsAddress = insAddress,
                AddressCalculationCrossedPageBoundary = didCrossPageBoundary
            };
        }

        public AddrModeCalcResult GetIndirectModeAddress(CPU cpu, Memory mem)
        {
            ushort operandAddress = cpu.FetchOperandWord(mem);
            // Get actual address from the operandAddress location
            return new AddrModeCalcResult
            {
                InsAddress = cpu.FetchWord(mem, operandAddress)
            };
        }

       public AddrModeCalcResult GetRelativeModeAddress(CPU cpu, Memory mem)
        {
            // The operand (signed byte) contains a relative address (positive or negative)
            return new AddrModeCalcResult
            {
                InsValue = cpu.FetchOperand(mem)
            };
        }

    }
}