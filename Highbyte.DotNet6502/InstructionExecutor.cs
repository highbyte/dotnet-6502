namespace Highbyte.DotNet6502;

/// <summary>
/// Executes a CPU instruction
/// </summary>
public class InstructionExecutor
{
    /// <summary>
    /// Executes the specified instruction.
    /// PC is assumed to point at the instruction operand, or the the next instruction, depending on instruction.
    /// When method returns, PC will be increased to point at next instruction 
    /// Returns true if instruction was handled, false is instruction is unknown.
    /// </summary>
    /// <param name="cpu"></param>
    /// <param name="mem"></param>
    /// <param name="opCode"></param>
    /// <returns></returns>
    public InstructionExecResult Execute(CPU cpu, Memory mem)
    {
        //if (cpu.PC == 0xff63 || cpu.PC == 0xE5AD)
        //    Debugger.Break();

        var atPC = cpu.PC;  // Remember the PC where the instruction is located, so we can return it in the result.

        byte opCode = cpu.FetchInstruction(mem);

        if (!cpu.InstructionList.OpCodeDictionary.ContainsKey(opCode))
            return InstructionExecResult.UnknownInstructionResult(opCode, atPC);

        var opCodeObject = cpu.InstructionList.GetOpCode(opCode);
        var instruction = cpu.InstructionList.GetInstruction(opCodeObject);

        // Derive what the final value is going to be used with the instruction based on addressing mode.
        // The way the addressing mode works is the same accross the instructions, so we don't need to repeat the logic
        // on how to get to the actual value used with the instruction.

        AddrModeCalcResult addrModeCalcResult = new AddrModeCalcResult() { OpCode = opCodeObject };
        addrModeCalcResult.OpCode = opCodeObject;
        addrModeCalcResult.AddressCalculationCrossedPageBoundary = false;
        addrModeCalcResult.InsAddress = null;
        addrModeCalcResult.InsValue = null;

        switch (opCodeObject.AddressingMode)
        {
            case AddrMode.I:
            {
                addrModeCalcResult.InsValue = cpu.FetchOperand(mem);
                break;
            }
            case AddrMode.ZP:
            {
                addrModeCalcResult.InsAddress = cpu.FetchOperand(mem);
                break;
            }
            case AddrMode.ZP_X:
            {
                addrModeCalcResult.InsAddress = cpu.CalcZeroPageAddressX(cpu.FetchOperand(mem), wrapZeroPage: true);
                break;
            }
            case AddrMode.ZP_Y:
            {
                addrModeCalcResult.InsAddress = cpu.CalcZeroPageAddressY(cpu.FetchOperand(mem), wrapZeroPage: true);
                break;
            }
            case AddrMode.ABS:
            {
                addrModeCalcResult.InsAddress = cpu.FetchOperandWord(mem);
                break;
            }
            case AddrMode.ABS_X:
            {
                // Note: CalcFullAddressX will check if adding X to address will cross page boundary. If so, one more cycle is consumed.
                addrModeCalcResult.InsAddress = cpu.CalcFullAddressX(cpu.FetchOperandWord(mem), out bool didCrossPageBoundary, false);
                addrModeCalcResult.AddressCalculationCrossedPageBoundary = didCrossPageBoundary;
                break;
            }
            case AddrMode.ABS_Y:
            {
                // Note: CalcFullAddressY will check if adding Y to address will cross page boundary. If so, one more cycle is consumed.
                addrModeCalcResult.InsAddress = cpu.CalcFullAddressY(cpu.FetchOperandWord(mem), out bool didCrossPageBoundary, false);
                addrModeCalcResult.AddressCalculationCrossedPageBoundary = didCrossPageBoundary;
                break;
            }
            case AddrMode.IX_IND:
            {
                addrModeCalcResult.InsAddress = cpu.FetchWord(mem, cpu.CalcZeroPageAddressX(cpu.FetchOperand(mem)));
                break;
            }
            case AddrMode.IND_IX:
            {
                addrModeCalcResult.InsAddress = cpu.CalcFullAddressY(cpu.FetchWord(mem, cpu.FetchOperand(mem)), out bool didCrossPageBoundary, false);
                addrModeCalcResult.AddressCalculationCrossedPageBoundary = didCrossPageBoundary;
                break;
            }
            case AddrMode.Indirect:
            {
                addrModeCalcResult.InsAddress = cpu.FetchWord(mem, cpu.FetchOperandWord(mem));
                break;
            }
            case AddrMode.Relative:
            {
                addrModeCalcResult.InsValue = cpu.FetchOperand(mem);
                break;
            }
            case AddrMode.Accumulator:
            {
                // This mode has no value or address
                break;
            }
            case AddrMode.Implied:
            {
                // This mode has no value or address
                break;
            }
            default:
                return InstructionExecResult.UnknownInstructionResult(opCode, atPC);
        }

        // Execute the instruction-specific logic, with final value calculated in addrModeCalcResult.
        ulong extraCyclesConsumed;
        if (instruction is IInstructionUsesByte instructionUsesByte)
        {
            // Instruction expects a byte directly or via a relative or absolute (word) address
            byte instructionValue;
            if (addrModeCalcResult.InsAddress.HasValue)
                instructionValue = cpu.FetchByte(mem, addrModeCalcResult.InsAddress.Value);
            else
                instructionValue = addrModeCalcResult.InsValue.Value;

            extraCyclesConsumed = instructionUsesByte.ExecuteWithByte(cpu, mem, instructionValue, addrModeCalcResult);
        }
        else if (instruction is IInstructionUsesAddress instructionUseAddress && addrModeCalcResult.InsAddress.HasValue)
        {
            // Instruction expects a an address (to write to, or use to change program counter)
            extraCyclesConsumed = instructionUseAddress.ExecuteWithWord(cpu, mem, addrModeCalcResult.InsAddress.Value, addrModeCalcResult);
        }
        else if (instruction is IInstructionUsesStack instructionUsesStack)
        {
            // Instruction is expected to push or pop stack
            extraCyclesConsumed = instructionUsesStack.ExecuteWithStack(cpu, mem, addrModeCalcResult);
        }
        else if (instruction is IInstructionUsesOnlyRegOrStatus instructionUseNone)
        {
            extraCyclesConsumed = instructionUseNone.Execute(cpu, addrModeCalcResult);
        }
        else
        {
            throw new DotNet6502Exception($"Bug detected. Did not find a way to execute instruction: {instruction.Name} opcode: {opCode.ToHex()}"); 
        }

        return InstructionExecResult.KnownInstructionResult(opCode, atPC, opCodeObject.MinimumCycles + extraCyclesConsumed);
    }
}
