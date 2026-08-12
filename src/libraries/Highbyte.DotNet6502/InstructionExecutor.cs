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

        // Per-model dispatch: one byte-indexed array lookup + one pre-composed handler
        // call. Addressing-mode resolution and instruction-interface selection were
        // decided at table build time (OpCodeDescriptorTableBuilder), not here.
        var descriptor = cpu.Descriptors[opCode];
        if (descriptor is null)
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

        var cyclesConsumed = descriptor.Execute(cpu, mem);
        if (cpu.IsHalted)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("CPU halted by unofficial instruction {OpCode} at {AtPC}", opCode.ToHex(), atPC.ToHex());
            return InstructionExecResult.HaltInstructionResult(opCode, atPC, cyclesConsumed);
        }

        return InstructionExecResult.KnownInstructionResult(opCode, atPC, cyclesConsumed);
    }
}
