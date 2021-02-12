namespace Highbyte.DotNet6502
{
    /// <summary>
    /// Use for instructions that only operates on, or needs, information in Registers or Status.
    /// </summary>
    public interface IInstructionUsesOnlyRegOrStatus
    {
        InstructionLogicResult Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult);
    }
}