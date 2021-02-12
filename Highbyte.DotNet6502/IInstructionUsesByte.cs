namespace Highbyte.DotNet6502
{
    /// <summary>
    /// </summary>
    public interface IInstructionUsesByte
    {
        InstructionLogicResult ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult);
    }
}