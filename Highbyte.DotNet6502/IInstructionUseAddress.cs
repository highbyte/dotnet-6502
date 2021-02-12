using System.Collections.Generic;

namespace Highbyte.DotNet6502
{
    /// <summary>
    /// </summary>
    public interface IInstructionUseAddress
    {
 
        InstructionLogicResult ExecuteWithWord(CPU cpu, Memory mem, ushort value, AddrModeCalcResult addrModeCalcResult);
    }
}