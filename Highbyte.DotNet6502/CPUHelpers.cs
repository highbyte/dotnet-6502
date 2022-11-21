namespace Highbyte.DotNet6502;

public static class CPUHelpers
{
    /// <summary>
    /// Gets the address of the next instruction after the current one. Or the address of the next instruction counted from a specified address.
    /// </summary>
    /// <param name="mem"></param>
    /// <param name="currentInstructionAddress"></param>
    /// <returns></returns>
    public static ushort GetNextInstructionAddress(this CPU cpu, Memory mem, ushort? currentInstructionAddress = null)
    {
        if (!currentInstructionAddress.HasValue)
            currentInstructionAddress = cpu.PC;
        byte insSize = GetInstructionSize(cpu, mem, currentInstructionAddress);
        ushort nextInstructionAddress = (ushort)(currentInstructionAddress + insSize);
        return nextInstructionAddress;
    }

    /// <summary>
    /// Gets the size in bytes of the current instruction, or the instruction at a specified address.
    /// If the instruction opcode is not known, it returns size of 1 byte.
    /// </summary>
    /// <param name="mem"></param>
    /// <param name="instructionAddress"></param>
    /// <returns></returns>
    public static byte GetInstructionSize(this CPU cpu, Memory mem, ushort? instructionAddress = null)
    {
        if (!instructionAddress.HasValue)
            instructionAddress = cpu.PC;
        var opCodeByte = mem[instructionAddress.Value];
        byte insSize;
        if (!cpu.InstructionList.OpCodeDictionary.ContainsKey(opCodeByte))
            insSize = 1;
        else
            insSize = (byte)cpu.InstructionList.GetOpCode(opCodeByte).Size;
        return insSize;
    }
}