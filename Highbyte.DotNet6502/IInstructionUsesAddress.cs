namespace Highbyte.DotNet6502
{
    /// <summary>
    /// Use for instructions requires an absolute address (word) value as input for processing.
    /// It includes instruction that may use the address to store values into memory, or change Program Counter to an absolute address.
    /// This does not include instructions that will use an absolute address to read a byte value as input for its logic. For those instructions, use IInstructionUsesByte)
    /// </summary>
    public interface IInstructionUsesAddress
    {
        InstructionLogicResult ExecuteWithWord(CPU cpu, Memory mem, ushort value, AddrModeCalcResult addrModeCalcResult);
    }
}