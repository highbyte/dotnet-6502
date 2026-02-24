using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Systems.Debugger;

/// <summary>
/// Unified IExecEvaluator used by both the internal monitor and the DAP debug adapter.
///
/// Combines:
/// - Monitor's BRK / unknown-instruction stop flags (converted to pre-execution checks)
/// - DebugAdapter's skip-first-check, step-over (temporary breakpoint at JSR return),
///   step-out (RTS detection), and HashSet-based address breakpoints.
/// - Optional per-address condition expressions evaluated by <see cref="BreakpointConditionEvaluator"/>.
///
/// All checks are pre-execution (reads mem[cpu.PC] before the instruction executes).
///
/// Threading: Both IExecEvaluator.Check() overloads delegate to CheckPreExecution().
/// The OnTriggered callback is invoked synchronously inside CheckPreExecution() BEFORE
/// the triggering flags (StepOutMode, TemporaryBreakpoint) are cleared, so the callback
/// can inspect them to determine the trigger reason (step-out vs. step-over vs. breakpoint).
/// </summary>
public class DebuggerBreakpointEvaluator : IExecEvaluator
{
    // -------------------------------------------------------------------------
    // Address-based breakpoints
    // Replaces Monitor's Dictionary<ushort, BreakPoint> and DAP's HashSet<ushort> _instructionBreakpoints.
    // -------------------------------------------------------------------------
    public HashSet<ushort> InstructionBreakpoints { get; } = new();

    /// <summary>
    /// Optional condition expressions per breakpoint address.
    /// When an address in <see cref="InstructionBreakpoints"/> (or matched by
    /// <see cref="AdditionalBreakAtAddress"/>) also has an entry here, the expression is
    /// evaluated by <see cref="BreakpointConditionEvaluator"/> before triggering.
    /// If the expression evaluates to <c>false</c> the breakpoint is skipped silently.
    /// Absent or null/empty = unconditional (always stop).
    /// </summary>
    public Dictionary<ushort, string?> BreakpointConditions { get; } = new();

    /// <summary>
    /// Optional hit count condition per breakpoint address.
    /// Syntax: a comparison operator followed by an integer:
    ///   "= N" or "N" — break when hit count equals N
    ///   ">= N"        — break when hit count is N or more
    ///   "> N"         — break when hit count exceeds N
    ///   "% N"         — break on every Nth hit (modulo)
    /// Absent or null/empty = no hit count filter.
    /// </summary>
    public Dictionary<ushort, string?> HitConditions { get; } = new();

    /// <summary>
    /// Running hit count per breakpoint address.
    /// Incremented each time the breakpoint address is reached (after expression condition passes).
    /// Reset when breakpoints are cleared or the debugger resets.
    /// </summary>
    public Dictionary<ushort, int> HitCounts { get; } = new();

    // -------------------------------------------------------------------------
    // Post-execution equivalent flags — now checked pre-execution.
    // Replaces Monitor's StopAfterBRKInstruction / StopAfterUnknownInstruction
    // (which were post-execution checks on InstructionExecResult).
    // -------------------------------------------------------------------------

    /// <summary>If true, stop when mem[cpu.PC] == 0x00 (BRK opcode) before executing it.</summary>
    public bool StopAfterBRKInstruction { get; set; } = false;

    /// <summary>If true, stop when the opcode at mem[cpu.PC] is not in the CPU's instruction list.</summary>
    public bool StopAfterUnknownInstruction { get; set; } = false;

    // -------------------------------------------------------------------------
    // Execution-flow flags (from DebugAdapter)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Skip the very next pre-execution check.
    /// Set by resume handlers (Continue, Next/JSR, StepOut) so that resuming from a
    /// breakpoint does not immediately re-trigger at the same address.
    /// </summary>
    public bool SkipNextBreakpointCheck { get; set; } = false;

    /// <summary>
    /// Set to the JSR return address (PC+3) for step-over.
    /// Cleared when the temporary breakpoint fires.
    /// </summary>
    public ushort? TemporaryBreakpoint { get; set; }

    /// <summary>
    /// When true, pause execution at the next RTS instruction (step-out).
    /// Cleared when the RTS is detected.
    /// </summary>
    public bool StepOutMode { get; set; } = false;

    // -------------------------------------------------------------------------
    // Extension point for callers that need additional checks (e.g. source breakpoints)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Optional additional breakpoint check by address.
    /// Used by the DAP debug adapter to check source-file breakpoints.
    /// Leave null in the internal monitor (which only does disassembly debugging).
    /// </summary>
    public Func<ushort, bool>? AdditionalBreakAtAddress { get; set; }

    // -------------------------------------------------------------------------
    // Logpoint support
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if the given address is a logpoint (should log a message but not stop).
    /// When set and returns true for a breakpoint address, execution continues and
    /// <see cref="OnLogpointHit"/> is invoked instead of <see cref="OnTriggered"/>.
    /// </summary>
    public Func<ushort, bool>? IsLogpoint { get; set; }

    /// <summary>
    /// Callback invoked synchronously when a logpoint fires.
    /// The address parameter is the PC where the logpoint matched.
    /// Unlike <see cref="OnTriggered"/>, this does NOT stop execution.
    /// </summary>
    public Action<ushort, CPU, Memory>? OnLogpointHit { get; set; }

    // -------------------------------------------------------------------------
    // Callback invoked synchronously inside CheckPreExecution when a trigger fires.
    // Called BEFORE the triggering flags (StepOutMode, TemporaryBreakpoint) are cleared,
    // so the callback can inspect them.
    //
    // The DAP debug adapter uses this to:
    //   - Execute the RTS instruction for step-out (cpu.ExecuteOneInstruction)
    //   - Set IsStopped = true synchronously (required so the Avalonia run loop pauses)
    //   - Fire-and-forget the appropriate DAP stopped event
    //
    // Leave null in the internal monitor (the trigger result propagates via
    // RunEmulatorOneFrame's return value, handled by OnAfterRunEmulatorOneFrame).
    // -------------------------------------------------------------------------
    public Action<ExecEvaluatorTriggerResult, CPU, Memory>? OnTriggered { get; set; }

    // -------------------------------------------------------------------------
    // IExecEvaluator implementation
    // -------------------------------------------------------------------------

    /// <summary>Pre-execution check: called with current CPU state before next instruction.</summary>
    public ExecEvaluatorTriggerResult Check(ExecState execState, CPU cpu, Memory mem)
        => CheckPreExecution(cpu, mem);

    /// <summary>Pre-execution check: also called with cpu.PC at next-to-execute instruction.</summary>
    public ExecEvaluatorTriggerResult Check(InstructionExecResult lastResult, CPU cpu, Memory mem)
        => CheckPreExecution(cpu, mem);

    // -------------------------------------------------------------------------
    // Direct-call entry point (used by DAP's built-in execution loop)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Direct pre-execution check. Returns the trigger result and fires OnTriggered if applicable.
    /// Used by DebugAdapterLogic.StartExecutionLoop() to check breakpoints inline without
    /// creating a dummy ExecState or InstructionExecResult.
    /// </summary>
    public ExecEvaluatorTriggerResult ShouldBreak(CPU cpu, Memory mem)
        => CheckPreExecution(cpu, mem);

    // -------------------------------------------------------------------------
    // Core logic
    // -------------------------------------------------------------------------

    private ExecEvaluatorTriggerResult CheckPreExecution(CPU cpu, Memory mem)
    {
        // Skip one check when resuming from a breakpoint so we don't immediately
        // re-trigger at the address we just stopped at.
        if (SkipNextBreakpointCheck)
        {
            SkipNextBreakpointCheck = false;
            return ExecEvaluatorTriggerResult.NotTriggered;
        }

        var pc = cpu.PC;
        byte opcode = mem[pc];

        // --- Step-out: stop at the RTS before executing it ---
        // Caller (DAP) is responsible for executing the RTS inside OnTriggered.
        if (StepOutMode && opcode == (byte)OpCodeId.RTS)
        {
            var result = ExecEvaluatorTriggerResult.CreateTrigger(
                ExecEvaluatorTriggerReasonType.DebugBreakPoint,
                $"Step-out RTS at ${pc:X4}");
            // Invoke callback BEFORE clearing flag so it can detect this was step-out.
            OnTriggered?.Invoke(result, cpu, mem);
            StepOutMode = false;
            return result;
        }

        // --- Step-over: temporary breakpoint at JSR return address ---
        if (TemporaryBreakpoint.HasValue && pc == TemporaryBreakpoint.Value)
        {
            var result = ExecEvaluatorTriggerResult.CreateTrigger(
                ExecEvaluatorTriggerReasonType.DebugBreakPoint,
                $"Step-over complete at ${pc:X4}");
            // Invoke callback BEFORE clearing flag so it can detect this was step-over.
            OnTriggered?.Invoke(result, cpu, mem);
            TemporaryBreakpoint = null;
            return result;
        }

        // --- User-set instruction breakpoints ---
        if (InstructionBreakpoints.Contains(pc))
        {
            // Skip if a condition is attached and evaluates to false.
            if (ConditionIsFalse(pc, cpu, mem))
                return ExecEvaluatorTriggerResult.NotTriggered;

            // Skip if a hit count condition is attached and not yet satisfied.
            if (HitConditionIsFalse(pc))
                return ExecEvaluatorTriggerResult.NotTriggered;

            // Logpoint: log message but don't stop execution
            if (IsLogpoint?.Invoke(pc) == true)
            {
                OnLogpointHit?.Invoke(pc, cpu, mem);
                return ExecEvaluatorTriggerResult.NotTriggered;
            }

            var result = ExecEvaluatorTriggerResult.CreateTrigger(
                ExecEvaluatorTriggerReasonType.DebugBreakPoint,
                $"Breakpoint at ${pc:X4}");
            OnTriggered?.Invoke(result, cpu, mem);
            return result;
        }

        // --- BRK instruction (pre-execution) ---
        if (StopAfterBRKInstruction && opcode == (byte)OpCodeId.BRK)
        {
            var result = ExecEvaluatorTriggerResult.CreateTrigger(
                ExecEvaluatorTriggerReasonType.BRKInstruction,
                $"BRK at ${pc:X4}");
            OnTriggered?.Invoke(result, cpu, mem);
            return result;
        }

        // --- Unknown instruction (pre-execution) ---
        if (StopAfterUnknownInstruction && !cpu.InstructionList.OpCodeDictionary.ContainsKey(opcode))
        {
            var result = ExecEvaluatorTriggerResult.CreateTrigger(
                ExecEvaluatorTriggerReasonType.UnknownInstruction,
                $"Unknown opcode ${opcode:X2} at ${pc:X4}");
            OnTriggered?.Invoke(result, cpu, mem);
            return result;
        }

        // --- Additional check (source breakpoints in DAP; null in Monitor) ---
        if (AdditionalBreakAtAddress?.Invoke(pc) == true)
        {
            // Skip if a condition is attached and evaluates to false.
            if (ConditionIsFalse(pc, cpu, mem))
                return ExecEvaluatorTriggerResult.NotTriggered;

            // Skip if a hit count condition is attached and not yet satisfied.
            if (HitConditionIsFalse(pc))
                return ExecEvaluatorTriggerResult.NotTriggered;

            // Logpoint: log message but don't stop execution
            if (IsLogpoint?.Invoke(pc) == true)
            {
                OnLogpointHit?.Invoke(pc, cpu, mem);
                return ExecEvaluatorTriggerResult.NotTriggered;
            }

            var result = ExecEvaluatorTriggerResult.CreateTrigger(
                ExecEvaluatorTriggerReasonType.DebugBreakPoint,
                $"Source breakpoint at ${pc:X4}");
            OnTriggered?.Invoke(result, cpu, mem);
            return result;
        }

        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    /// <summary>
    /// Returns true when a condition string is attached to <paramref name="pc"/> and
    /// evaluates to false (meaning the breakpoint should be skipped).
    /// Returns false when there is no condition or the condition evaluates to true.
    /// </summary>
    private bool ConditionIsFalse(ushort pc, CPU cpu, Memory mem)
        => BreakpointConditions.TryGetValue(pc, out var cond)
           && !string.IsNullOrEmpty(cond)
           && !BreakpointConditionEvaluator.Evaluate(cond, cpu, mem);

    /// <summary>
    /// Increments the hit count for <paramref name="pc"/> and returns true when a
    /// hit count condition is attached and the current count does NOT satisfy it
    /// (meaning the breakpoint should be skipped).
    /// Returns false when there is no hit condition or the condition is satisfied.
    /// </summary>
    private bool HitConditionIsFalse(ushort pc)
    {
        if (!HitConditions.TryGetValue(pc, out var hitCond) || string.IsNullOrWhiteSpace(hitCond))
            return false; // No hit condition — don't skip

        // Increment the running count for this address
        HitCounts.TryGetValue(pc, out var count);
        count++;
        HitCounts[pc] = count;

        // Parse the hit condition: optional operator followed by integer
        var cond = hitCond.Trim();

        if (cond.StartsWith(">="))
        {
            if (int.TryParse(cond.Substring(2).Trim(), out var n))
                return count < n; // skip while count < n
        }
        else if (cond.StartsWith(">"))
        {
            if (int.TryParse(cond.Substring(1).Trim(), out var n))
                return count <= n; // skip while count <= n
        }
        else if (cond.StartsWith("%"))
        {
            if (int.TryParse(cond.Substring(1).Trim(), out var n) && n > 0)
                return (count % n) != 0; // skip unless count is a multiple of n
        }
        else if (cond.StartsWith("="))
        {
            if (int.TryParse(cond.Substring(1).Trim(), out var n))
                return count != n; // skip unless count equals n
        }
        else
        {
            // Bare number: treat as "= N"
            if (int.TryParse(cond, out var n))
                return count != n;
        }

        return false; // Unparseable — don't skip (fail-safe)
    }
}
