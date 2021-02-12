namespace Highbyte.DotNet6502
{
    /// <summary>
    /// Use for instructions requires a byte value as input for processing.
    /// The byte value can come as an immediate value in the operand, or via a relative (signed byte) or absolute (word) adddress.
    /// The logic that pre-processes the instruction is responsible to provide the final byte value, however the addressing mode dictates.
    /// The instruction that implements this interface will be provided with the final value.
    /// </summary>
    public interface IInstructionUsesByte
    {
        InstructionLogicResult ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult);
    }
}