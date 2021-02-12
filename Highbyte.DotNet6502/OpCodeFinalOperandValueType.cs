namespace Highbyte.DotNet6502
{
    /// <summary>
    /// The type of value (if any) an instruction expects as to operate is logic on (after addressing modes has possible found the final value).
    /// </summary>
    public enum OpCodeFinalOperandValueType
    {
        /// <summary>
        /// Expects a byte read from memory, or a immediate value
        /// </summary>        
        ReadByte,
        /// <summary>
        /// Expects a 16-bit (word) address to write a byte to
        /// </summary>        
        WriteByte,
        /// <summary>
        /// Expects a 16-bit (word) address
        /// </summary>        
        Address,
        /// <summary>
        /// No operand.
        /// </summary>        
        None,
    }
}
