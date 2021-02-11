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

        /// <summary>
        /// Reads instruction from current PC and executes it.
        /// When method returns, PC will be increased to point at next instruction 
        /// Returns true if instruction was handled, false is instruction is unknown.
        /// </summary>
        /// <param name="cpu"></param>
        /// <param name="mem"></param>
        public void Execute(CPU cpu, Memory mem)
        {
            byte opCode = cpu.FetchInstruction(mem);
            Execute(cpu, mem, opCode);
        }

        /// <summary>
        /// Executes the specified instruction.
        /// PC is assumed to point at the instruction operand, or the the next instruction, depending on instruction.
        /// When method returns, PC will be increased to point at next instruction 
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

            AddrModeCalcResult addrModeCalcResult = new AddrModeCalcResult(opCodeObject);
            switch(opCodeObject.AddressingMode)
            {
                case AddrMode.I:
                {
                    addrModeCalcResult.InsValue = cpu.FetchOperand(mem);
                    break;
                }
                case AddrMode.ZP:
                {
                    addrModeCalcResult.InsAddress = cpu.FetchOperand(mem);
                    break;
                }
                case AddrMode.ZP_X:
                {
                    addrModeCalcResult.InsAddress = cpu.CalcZeroPageAddressX(cpu.FetchOperand(mem), wrapZeroPage: true);
                    break;
                }
                case AddrMode.ZP_Y:
                {
                    addrModeCalcResult.InsAddress = cpu.CalcZeroPageAddressY(cpu.FetchOperand(mem), wrapZeroPage: true);
                    break;
                }
                case AddrMode.ABS:
                {
                    addrModeCalcResult.InsAddress = cpu.FetchOperandWord(mem);
                    break;
                }
                case AddrMode.ABS_X:
                {
                    // Note: CalcFullAddressX will check if adding X to address will cross page boundary. If so, one more cycle is consumed.
                    addrModeCalcResult.InsAddress = cpu.CalcFullAddressX(cpu.FetchOperandWord(mem), out bool didCrossPageBoundary, false);
                    addrModeCalcResult.AddressCalculationCrossedPageBoundary = didCrossPageBoundary;
                    break;
                }
                case AddrMode.ABS_Y:
                {
                    // Note: CalcFullAddressY will check if adding Y to address will cross page boundary. If so, one more cycle is consumed.
                    addrModeCalcResult.InsAddress = cpu.CalcFullAddressY(cpu.FetchOperandWord(mem), out bool didCrossPageBoundary, false);
                    addrModeCalcResult.AddressCalculationCrossedPageBoundary = didCrossPageBoundary;
                    break;
                }
                case AddrMode.IX_IND:
                {
                    addrModeCalcResult.InsAddress = cpu.FetchWord(mem, cpu.CalcZeroPageAddressX(cpu.FetchOperand(mem)));
                    break;
                }
                case AddrMode.IND_IX:
                {
                    addrModeCalcResult.InsAddress = cpu.CalcFullAddressY(cpu.FetchWord(mem, cpu.FetchOperand(mem)), out bool didCrossPageBoundary, false);
                    addrModeCalcResult.AddressCalculationCrossedPageBoundary = didCrossPageBoundary;
                    break;
                }
                case AddrMode.Indirect:
                {
                    addrModeCalcResult.InsAddress = cpu.FetchWord(mem, cpu.FetchOperandWord(mem));
                    break;
                }
                case AddrMode.Relative:
                {
                    addrModeCalcResult.InsValue = cpu.FetchOperand(mem);
                    break;
                }
                case AddrMode.Accumulator:
                {
                    // This mode has no value or address
                    break;
                }
                case AddrMode.Implied:
                {
                    // This mode has no value or address
                    break;
                }
                 default:
                    return false;
            }

            if(addrModeCalcResult==null)
                throw new DotNet6502Exception("Bug detected. Variable addrModeCalcResult expected to be set.");

            return instruction.Execute(cpu, mem, addrModeCalcResult);
        }
    }
}