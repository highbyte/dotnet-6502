namespace Highbyte.DotNet6502.Utils;

/// <summary>
/// Helper class formatting output for instructions
/// </summary>
public static class OutputGen
{
    private const string HexPrefix = "";
    /// <summary>
    /// Returns a string with the following information
    /// [last instruction PC]  [byte1 [byte2 [byte3]]]  [instruction] [addressingmode/value] 
    /// </summary>
    /// <param name="cpu"></param>
    /// <param name="mem"></param>
    /// <returns></returns>
    public static string GetLastInstructionDisassembly(CPU cpu, Memory mem)
    {
        var programAddress = cpu.ExecState.PCBeforeLastOpCodeExecuted;
        if (programAddress.HasValue)
            return GetInstructionDisassembly(cpu, mem, programAddress.Value);
        else
            throw new DotNet6502Exception("programAddress is null");
    }

    /// <summary>
    /// Returns a string with the following information
    /// [next instruction PC]  [byte1 [byte2 [byte3]]]  [instruction] [addressingmode/value] 
    /// </summary>
    /// <param name="cpu"></param>
    /// <param name="mem"></param>
    /// <returns></returns>
    public static string GetNextInstructionDisassembly(CPU cpu, Memory mem)
    {
        var programAddress = cpu.PC;
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
        var addressString = $"{address.ToHex(HexPrefix, lowerCase: true)}";
        var memoryString = BuildMemoryString(cpu, mem, address);
        var instructionString = BuildInstructionString(cpu, mem, address);

        return $"{addressString}  {memoryString,-8}  {instructionString,-11}";
    }

    public static string BuildMemoryString(CPU cpu, Memory mem, ushort address)
    {
        // Model-aware: per-byte metadata comes from the CPU's descriptor table, so the
        // same byte disassembles per the CPU's actual model ($9C: SHY/undefined on NMOS
        // profiles, STZ abs on a 65C02).
        var opCodeByte = mem[address];
        if (!cpu.IsOpCodeDefined(opCodeByte))
            return $"{opCodeByte.ToHex(HexPrefix, lowerCase: true)}";

        var size = cpu.GetOpCodeSize(opCodeByte);
        var operand = mem.ReadData((ushort)(address + 1), (ushort)(size - 1)); // -1 for the opcode itself

        return $"{opCodeByte.ToHex(HexPrefix, lowerCase: true)} {string.Join(" ", operand.Select(x => x.ToHex(HexPrefix, lowerCase: true)))}";
    }

    public static string BuildInstructionString(CPU cpu, Memory mem, ushort address)
    {
        var opCodeByte = mem[address];
        var descriptor = cpu.Descriptors[opCodeByte];
        if (descriptor is null)
            return "???";

        var operand = mem.ReadData((ushort)(address + 1), (ushort)(descriptor.Size - 1)); // -1 for the opcode itself

        var operandString = BuildOperandString(descriptor.Addressing, operand);
        return $"{descriptor.Mnemonic} {operandString}";
    }

    public static string BuildInstructionName(CPU cpu, byte opCode)
    {
        // Model-aware: the mnemonic per the CPU's own model.
        return cpu.Descriptors[opCode]?.Mnemonic ?? "???";
    }

    public static string BuildOperandString(AddrMode addrMode, byte[] operand)
    {
        switch (addrMode)
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
                return $"*{(offset >= 0 ? "+" : "")}{offset}";
            }
            case AddrMode.Accumulator:
            {
                return "A";
            }
            case AddrMode.Implied:
            {
                return "";
            }
            case AddrMode.ZP_IND:
            {
                return $"(${operand[0].ToHex(HexPrefix)})";
            }
            case AddrMode.ABS_IX_IND:
            {
                return $"(${operand.ToLittleEndianWord().ToHex(HexPrefix)},X)";
            }
            default:
                throw new DotNet6502Exception($"Bug detected! Unhandled addressing mode: {addrMode}");
        }
    }

    public static string GetProcessorState(CPU cpu, bool includeCycles = false)
    {
        return $"{GetRegisters(cpu)} {GetStatus(cpu)} {GetPCandSP(cpu)}{(includeCycles ? " CY=" + cpu.ExecState.CyclesConsumed : "")}";
    }

    public static Dictionary<string, string> GetProcessorStateDictionary(CPU cpu, bool includeCycles = false)
    {
        var state = new Dictionary<string, string>
        {
            { "A", cpu.A.ToHex(HexPrefix) },
            { "X", cpu.X.ToHex(HexPrefix) },
            { "Y", cpu.Y.ToHex(HexPrefix) },
            { "PS", GetStatusValueString(cpu) },
            { "PC", cpu.PC.ToHex(HexPrefix) },
            { "SP", cpu.SP.ToHex(HexPrefix) }
        };
        if (includeCycles)
            state.Add("CY", cpu.ExecState.CyclesConsumed.ToString());
        return state;
    }

    public static string GetRegisters(CPU cpu)
    {
        return $"A={cpu.A.ToHex(HexPrefix)} X={cpu.X.ToHex(HexPrefix)} Y={cpu.Y.ToHex(HexPrefix)}";
    }

    public static string GetStatus(CPU cpu)
    {
        return "PS=["
        + GetStatusValueString(cpu)
        + "]";
    }
    private static string GetStatusValueString(CPU cpu)
    {
        return ""
        + (cpu.ProcessorStatus.Negative ? "N" : "-") // Bit 7
        + (cpu.ProcessorStatus.Overflow ? "V" : "-")
        + (cpu.ProcessorStatus.Unused ? "U" : "-")
        + (cpu.ProcessorStatus.Break ? "B" : "-")
        + (cpu.ProcessorStatus.Decimal ? "D" : "-")
        + (cpu.ProcessorStatus.InterruptDisable ? "I" : "-")
        + (cpu.ProcessorStatus.Zero ? "Z" : "-")
        + (cpu.ProcessorStatus.Carry ? "C" : "-"); // Bit 0

    }

    public static string GetPCandSP(CPU cpu)
    {
        return $"SP={cpu.SP.ToHex(HexPrefix)} PC={cpu.PC.ToHex(HexPrefix)}";
    }
}
