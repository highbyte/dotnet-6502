using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502;

/// <summary>
/// Executes a CPU instruction
/// </summary>
public class InstructionExecutor
{
    private readonly ILogger _logger;

    public InstructionExecutor(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(typeof(InstructionExecutor).Name);
    }

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
        var atPC = cpu.PC;  // Remember the PC where the instruction is located, so we can return it in the result.

        byte opCode = cpu.FetchInstruction(mem);

        // Single byte-indexed array lookup replaces ContainsKey + Dictionary[] -- a 3x
        // dictionary-lookup hot spot on the per-instruction path.
        var opCodeObject = cpu.InstructionList.TryGetOpCode(opCode);
        if (opCodeObject is null)
        {
            // Guard the LogWarning behind IsEnabled. The unknown-opcode path is hit on every
            // emulated occurrence of an undocumented 6502 opcode (which real games and demos
            // do use), so it sits on the per-instruction hot path for those workloads. The
            // .ToHex() calls allocate two short strings each call; the guard skips that
            // allocation entirely when warning-level logging is filtered out.
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Unknown instruction {OpCode} at {AtPC}", opCode.ToHex(), atPC.ToHex());
            return InstructionExecResult.UnknownInstructionResult(opCode, atPC);
        }

        var instruction = cpu.InstructionList.GetInstruction(opCodeObject);

        // Derive what the final value is going to be used with the instruction based on addressing mode.
        // The way the addressing mode works is the same accross the instructions, so we don't need to repeat the logic
        // on how to get to the actual value used with the instruction.

        AddrModeCalcResult addrModeCalcResult = new AddrModeCalcResult() { OpCode = opCodeObject };

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
                addrModeCalcResult.InsAddress = cpu.CalcFullAddressX(cpu.FetchOperandWord(mem), out bool didCrossPageBoundary);
                addrModeCalcResult.AddressCalculationCrossedPageBoundary = didCrossPageBoundary;
                break;
            }
            case AddrMode.ABS_Y:
            {
                // Note: CalcFullAddressY will check if adding Y to address will cross page boundary. If so, one more cycle is consumed.
                addrModeCalcResult.InsAddress = cpu.CalcFullAddressY(cpu.FetchOperandWord(mem), out bool didCrossPageBoundary);
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
                addrModeCalcResult.InsAddress = cpu.CalcFullAddressY(cpu.FetchWord(mem, cpu.FetchOperand(mem)), out bool didCrossPageBoundary);
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
        var extraCyclesConsumed = ExecuteInstruction(cpu, mem, instruction, addrModeCalcResult, opCode);

        var cyclesConsumed = opCodeObject.MinimumCycles + extraCyclesConsumed;
        if (cpu.IsHalted)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("CPU halted by unofficial instruction {OpCode} at {AtPC}", opCode.ToHex(), atPC.ToHex());
            return InstructionExecResult.HaltInstructionResult(opCode, atPC, cyclesConsumed);
        }

        return InstructionExecResult.KnownInstructionResult(opCode, atPC, cyclesConsumed);
    }

    private static ulong ExecuteInstruction(CPU cpu, Memory mem, Instruction instruction, AddrModeCalcResult addrModeCalcResult, byte opCode)
    {
        if (instruction is IInstructionUsesByte instructionUsesByte)
        {
            return instructionUsesByte.ExecuteWithByte(cpu, mem, GetInstructionValue(cpu, mem, addrModeCalcResult), addrModeCalcResult);
        }

        if (instruction is IInstructionUsesAddress instructionUseAddress && addrModeCalcResult.InsAddress.HasValue)
        {
            // Instruction expects an address to write to, or use to change program counter.
            return instructionUseAddress.ExecuteWithWord(cpu, mem, addrModeCalcResult.InsAddress.Value, addrModeCalcResult);
        }

        if (instruction is IInstructionUsesStack instructionUsesStack)
        {
            return instructionUsesStack.ExecuteWithStack(cpu, mem, addrModeCalcResult);
        }

        if (instruction is IInstructionUsesOnlyRegOrStatus instructionUseNone)
        {
            return instructionUseNone.Execute(cpu, addrModeCalcResult);
        }

        throw new DotNet6502Exception($"Bug detected. Did not find a way to execute instruction: {instruction.Name} opcode: {opCode.ToHex()}");
    }

    private static byte GetInstructionValue(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
    {
        // Instruction expects a byte directly or via a relative or absolute (word) address.
        if (addrModeCalcResult.InsAddress.HasValue)
            return cpu.FetchByte(mem, addrModeCalcResult.InsAddress.Value);

        return addrModeCalcResult.InsValue!.Value;
    }
}
