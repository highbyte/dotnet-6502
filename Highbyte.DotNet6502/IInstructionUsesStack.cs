namespace Highbyte.DotNet6502
{
    /// <summary>
    /// </summary>
    public interface IInstructionUsesStack
    {
        InstructionLogicResult ExecuteWithStack(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult);
    }
}