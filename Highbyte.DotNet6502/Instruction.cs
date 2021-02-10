using System.Collections.Generic;

namespace Highbyte.DotNet6502
{
    /// <summary>
    /// </summary>
    public abstract class Instruction
    {
        public virtual string Name => this.GetType().Name;
        public abstract List<OpCode> OpCodes { get; }

        /// <summary>
        /// Executes an instruction
        /// </summary>
        /// <param name="cpu"></param>
        /// <param name="mem"></param>
        /// <param name="addrModeCalcResult"></param>
        public virtual bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            return false;
        }

        protected byte GetInstructionValueFromAddressOrDirectly(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            byte insValue;
            if(addrModeCalcResult.InsAddress.HasValue)
            {
                insValue = cpu.FetchByte(mem, addrModeCalcResult.InsAddress.Value);
            }
            else
                insValue = addrModeCalcResult.InsValue.Value;
            return insValue;         
        }
    }
}
