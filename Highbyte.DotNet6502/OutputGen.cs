using System.Linq;

namespace Highbyte.DotNet6502
{
    /// <summary>
    /// Helper class formatting output
    /// </summary>
    public static class OutputGen
    {
        public static string FormatLastInstruction(CPU cpu, Memory mem)
        {
            ushort programAddress = cpu.ExecState.PCBeforeLastOpCodeExecuted.Value;

            byte opCode = cpu.ExecState.LastOpCode.Value;
            var opCodeObject = cpu.InstructionList.OpCodeDictionary[opCode];
            // Check if instruction is recognized
            if(opCodeObject==null)
                return FormatInstruction(programAddress, opCode);

            var operand = mem.ReadData((ushort)(programAddress + 1), (ushort)(opCodeObject.Size - 1)); // -1 for the opcode itself
            return FormatInstruction(programAddress, opCode, operand);
        }
 
        // TODO: Format output string according to addressing code convention
        public static string FormatInstruction(ushort address, byte opCode, byte[] operand = null)
        {
            string opCodeString;
            if(opCode.IsDefinedAsOpCodeId())
                opCodeString = opCode.ToOpCodeId().ToString();
            else
                opCodeString = opCode.ToHex();

            string operandString;
            if(operand==null)
                operandString = "";
            else
                operandString = string.Join(" ", operand.Select(x=>x.ToHex()));

            return $"{address.ToHex()}: {opCodeString, -8} {operandString, -9}";
        }
    }
}