namespace Highbyte.DotNet6502
{
    /// <summary>
    /// Use for instructions that push or pop the stack.
    /// </summary>
    public interface IInstructionUsesStack
    {
        InstructionLogicResult ExecuteWithStack(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult);
    }
}