namespace Highbyte.DotNet6502
{
    /// <summary>
    /// </summary>
    public interface IInstructionUseNone
    {
        InstructionLogicResult Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult);
    }
}