using System;
using System.Linq;

namespace Highbyte.DotNet6502
{
    /// <summary>
    /// Helper class formatting output
    /// </summary>
    public static class OutputGen
    {
        const string HexPrefix = "";
        /// <summary>
        /// Returns a string with the following information
        /// [last instruction PC]  [byte1 [byte2 [byte3]]]  [instruction] [addressingmode/value] 
        /// </summary>
        /// <param name="cpu"></param>
        /// <param name="mem"></param>
        /// <returns></returns>
        public static string GetLastInstructionDisassembly(CPU cpu, Memory mem)
        {
            ushort programAddress = cpu.ExecState.PCBeforeLastOpCodeExecuted.Value;
            return GetInstructionDisassembly(cpu, mem, programAddress);
        }
 
        /// <summary>
        /// Returns a string with the following information
        /// [address]  [byte1 [byte2 [byte3]]]  [instruction] [addressingmode/value] 
        /// </summary>
        /// <param name="cpu"></param>
        /// <param name="address"></param>
        /// <param name="opCode"></param>
        /// <param name="operand"></param>
        /// <returns></returns>
        public static string GetInstructionDisassembly(CPU cpu, Memory mem, ushort address)
        {
            byte opCodeByte = mem[address];
            OpCode opCode;
            byte[] operand;
            string memoryString;
            string instructionString;
            string operandString;
            if(cpu.InstructionList.OpCodeDictionary.ContainsKey(opCodeByte))
            {
                opCode = cpu.InstructionList.GetOpCode(opCodeByte);
                operand = mem.ReadData((ushort)(address + 1), (ushort)(opCode.Size - 1)); // -1 for the opcode itself
                memoryString = $"{opCodeByte.ToHex(HexPrefix)} {string.Join(" ", operand.Select(x=>x.ToHex(HexPrefix)))}";
                instructionString = BuildInstructionString(cpu, opCode);
                operandString = BuildOperandString(opCode.AddressingMode, operand);
            }
            else
            {
                operand = Array.Empty<byte>();
                memoryString = string.Empty;
                instructionString = "???";
                operandString = "";
            }

            string addressString = $"{address.ToHex(HexPrefix)}";

            return $"{addressString}  {memoryString,-8}  {instructionString} {operandString,-7}";
        }

        public static string BuildInstructionString(CPU cpu, OpCode opCode)
        {
            var instruction = cpu.InstructionList.GetInstruction(opCode);
            return instruction.Name;
        }

        public static string BuildOperandString(AddrMode addrMode, byte[] operand)
        {
            switch(addrMode)
            {
                case AddrMode.I:
                {
                    return $"#${operand[0].ToHex(HexPrefix)}";
                }
                case AddrMode.ZP:
                {
                    return $"${operand[0].ToHex(HexPrefix)}";
                }
                case AddrMode.ZP_X:
                {
                    return $"${operand[0].ToHex(HexPrefix)},X";
                }
                case AddrMode.ZP_Y:
                {
                    return $"${operand[0].ToHex(HexPrefix)},Y";
                }
                case AddrMode.ABS:
                {
                    return $"${operand.ToLittleEndianWord().ToHex(HexPrefix)}";
                }
                case AddrMode.ABS_X:
                {
                    return $"${operand.ToLittleEndianWord().ToHex(HexPrefix)},X";
                }
                case AddrMode.ABS_Y:
                {
                    return $"${operand.ToLittleEndianWord().ToHex(HexPrefix)},Y";
                }
                case AddrMode.IX_IND:
                {
                    return $"(${operand[0].ToHex(HexPrefix)},X)";
                }
                case AddrMode.IND_IX:
                {
                    return $"(${operand[0].ToHex(HexPrefix)}),Y";
                }
                case AddrMode.Indirect:
                {
                    return $"(${operand.ToLittleEndianWord().ToHex(HexPrefix)})";
                }
                case AddrMode.Relative:
                {
                    var offset = (sbyte)operand[0];
                    return $"*{(offset>=0?"+":"")}{offset}";
                }
                case AddrMode.Accumulator:
                {
                    return "A";
                }
                case AddrMode.Implied:
                {
                    return "";
                }
                default:
                    throw new DotNet6502Exception($"Bug detected! Unhandled addressing mode: {addrMode}");
            }
        }
    }
}