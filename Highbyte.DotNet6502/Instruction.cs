using System.Collections.Generic;

namespace Highbyte.DotNet6502
{
    /// <summary>
    /// </summary>
    public abstract class Instruction
    {
        public virtual string Name => GetType().Name;
        public abstract List<OpCode> OpCodes { get; }

        public bool SupportsAddressingMode(AddrMode mode)
        {
            return OpCodes.Exists(x=>x.AddressingMode == mode);
        }
    }

    /// <summary>
    /// Use for instructions requires a byte value as input for processing.
    /// The byte value can come as an immediate value in the operand, or via a relative (signed byte) or absolute (word) adddress.
    /// The logic that pre-processes the instruction is responsible to provide the final byte value, however the addressing mode dictates.
    /// The instruction that implements this interface will be provided with the final value.
    /// </summary>
    public interface IInstructionUsesByte
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cpu"></param>
        /// <param name="mem"></param>
        /// <param name="value"></param>
        /// <param name="addrModeCalcResult"></param>
        /// <returns>Number of extra CPU cycles used in addition to the minimul defined by the OpCode.</returns>
        ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult);
    }

    /// <summary>
    /// Use for instructions requires an absolute address (word) value as input for processing.
    /// It includes instruction that may use the address to store values into memory, or change Program Counter to an absolute address.
    /// This does not include instructions that will use an absolute address to read a byte value as input for its logic. For those instructions, use IInstructionUsesByte)
    /// </summary>
    public interface IInstructionUsesAddress
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cpu"></param>
        /// <param name="mem"></param>
        /// <param name="value"></param>
        /// <param name="addrModeCalcResult"></param>
        /// <returns>Number of extra CPU cycles used in addition to the minimul defined by the OpCode.</returns>
        ulong ExecuteWithWord(CPU cpu, Memory mem, ushort value, AddrModeCalcResult addrModeCalcResult);
    }

    /// <summary>
    /// Use for instructions that push or pop the stack.
    /// </summary>
    public interface IInstructionUsesStack
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cpu"></param>
        /// <param name="mem"></param>
        /// <param name="addrModeCalcResult"></param>
        /// <returns>Number of extra CPU cycles used in addition to the minimul defined by the OpCode.</returns>
        ulong ExecuteWithStack(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult);
    }

    /// <summary>
    /// Use for instructions that only operates on, or needs, information in Registers or Status.
    /// </summary>
    public interface IInstructionUsesOnlyRegOrStatus
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cpu"></param>
        /// <param name="addrModeCalcResult"></param>
        /// <returns>Number of extra CPU cycles used in addition to the minimul defined by the OpCode.</returns>
        ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult);
    }
}
