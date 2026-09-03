using System.Diagnostics;
using System.Runtime.CompilerServices;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502;

public class CPU
{
    /// <summary>
    /// Program Counter
    /// </summary>
    public ushort PC;

    /// <summary>
    /// Stack Pointer
    /// The 6502 microprocessor supports a 256 byte stack fixed between memory locations $0100 and $01FF. 
    /// A special 8-bit register, S, is used to keep track of the next free byte of stack space. 
    /// Pushing a byte on to the stack causes the value to be stored at the current free location (e.g. $0100,S) 
    /// and then the stack pointer is post decremented. 
    /// Pull operations reverse this procedure.
    /// 
    /// The stack register can only be accessed by transferring its value to or from the X register via instructions TSX and TXS.
    /// Its value is automatically modified by push/pull instructions, subroutine calls and returns, interrupts and returns from interrupts.
    /// 
    /// Other instructions for storing values on stack: PHA, PHP, PLA, PLP
    /// </summary>
    public byte SP;

    /// <summary>
    /// Accumulator
    /// </summary>
    public byte A;

    /// <summary>
    /// Index Register X
    /// </summary>
    public byte X;

    /// <summary>
    /// Index Register Y
    /// </summary>
    public byte Y;

    /// <summary>
    /// Processor Status.
    /// 
    /// As instructions are executed a set of processor flags are set or clear to record the results of the operation. This flags and some additional control flags are held in a special status register. Each flag has a single bit within the register.
    /// 
    /// Instructions exist to test the values of the various bits, to set or clear some of them and to push or pull the entire set to or from the stack.
    /// </summary>
    public ProcessorStatus ProcessorStatus;

    /// <summary>
    /// Address for vector to Non-maskable interrupt handler at 0xfffa/0xfffb
    /// </summary>
    public const ushort StackBaseAddress = 0x0100; // Stack memory: 0x0100 - 0x01ff

    /// <summary>
    /// Address for vector to Non-maskable interrupt handler at 0xfffa/0xfffb
    /// </summary>
    public const ushort NonMaskableIRQHandlerVector = 0xfffa; // + 0xfffb

    /// <summary>
    /// Address for vector to Power on reset location at 0xfffc/0xfffd
    /// </summary>
    public const ushort ResetVector = 0xfffc; // + 0xfffd

    /// <summary>
    ///  Address for vector to BRK and IRQ (interrupt request) handler
    /// </summary>
    public const ushort BrkIRQHandlerVector = 0xfffe; // + 0xffff

    public CPUInterrupts CPUInterrupts { get; private set; } = new CPUInterrupts();

    /// <summary>
    /// The bus cycle at which the last instruction polled its interrupt lines. The 6502 samples
    /// IRQ and NMI at the end of an instruction's second-to-last cycle (for a taken branch that
    /// does not cross a page, at the end of its first cycle), so a line that goes active during
    /// the last cycle is only seen after the following instruction. A device reports the cycle it
    /// asserted the line on (see <see cref="CPUInterrupts.SetIRQSourceActive(string, bool, ulong)"/>);
    /// an interrupt is taken at a boundary only if that cycle is at or before this one.
    /// </summary>
    private ulong _interruptPollBusCycle;

    /// <summary>
    /// The I flag as seen by the last poll, when it differs from the live flag: CLI, SEI and PLP
    /// change the flag after the poll, so the interrupt decision at their boundary uses the value
    /// from before the instruction. Null means "use the live flag" (RTI changes it in time).
    /// </summary>
    private bool? _interruptDisableAtPoll;

    /// <summary>
    /// Is True when a IRQ (Interrupt Request) has been raised.
    /// Raising a NMI is done by setting a NMI source active, which is done by calling CPUInterrupts.SetNMISourceActive(source).
    /// 
    /// Will trigger IRQ processing after current instruction has been executed, which will end in the ProgramCounter (PC) being set to the IRQ vector address defined in 0xfffe.
    /// </summary>
    /// <value></value>

    public bool IRQ => CPUInterrupts.IRQLineEnabled;

    /// <summary>
    /// Is True when a NMI (non-maskable interrupt) has been raised.
    /// Raising a NMI is done by setting a NMI source active, which is done by calling CPUInterrupts.SetNMISourceActive(source).
    /// 
    /// Will trigger NMI processing after current instruction has been executed, which will end in the ProgramCounter (PC) being set to the IRQ vector address defined in 0xfffa.
    /// </summary>
    /// <value></value>

    public bool NMI => CPUInterrupts.NMIPending;

    /// <summary>
    /// Aggregated stats and info for all invocations of Execute()
    /// </summary>
    /// <value></value>
    public ExecState ExecState { get; private set; }
    public bool IsHalted { get; private set; }

    /// <summary>
    /// Bus cycles performed by this CPU: one per byte read or written through the CPU's own
    /// access helpers (<see cref="FetchByte"/>, <see cref="StoreByte"/>). On a 6502 every clock
    /// cycle is a bus access, so once every instruction performs all of its accesses (dummy reads
    /// included) the count advanced by an instruction equals its cycle count; the tests hold the
    /// two together. Monotonic; not reset by <see cref="Reset"/>.
    /// </summary>
    public ulong BusCycles { get; private set; }

    /// <summary>
    /// Optional bus master that can stall reads (see <see cref="IBusStallSource"/>). While a read is
    /// stalled <see cref="BusCycles"/> advances without an access, so the count is then the cycle
    /// count, of which the accesses are a subset. Stall cycles are added to the instruction's
    /// reported cycles.
    /// </summary>
    public IBusStallSource? BusStallSource
    {
        get => _busStallSource;
        set
        {
            _busStallSource = value;
            _readStallCheckFromBusCycle = value is null ? ulong.MaxValue : 0;
        }
    }
    private IBusStallSource? _busStallSource;

    // The earliest bus cycle at which a read must consult the stall source (ulong.MaxValue: none).
    private ulong _readStallCheckFromBusCycle = ulong.MaxValue;

    // Stall cycles accumulated since the current instruction (or interrupt entry) began.
    private ulong _stallCycles;

    /// <summary>
    /// Ask the stall source again on the next read. Systems call this when the state that decides
    /// stalls changes (a VIC-II register write, a frame wrap, a snapshot restore).
    /// </summary>
    public void RequestBusStallCheck()
    {
        if (_busStallSource is not null)
            _readStallCheckFromBusCycle = 0;
    }

    public CpuCompatibilityProfile CompatibilityProfile { get; private set; }

    /// <summary>
    /// The immutable CPU model definition this CPU was constructed with. Selected once
    /// here and never consulted on the per-instruction path.
    /// </summary>
    internal CpuModelDefinition ModelDefinition { get; private set; }

    /// <summary>
    /// Per-CPU-instance state beyond the standard registers for models that have any
    /// (e.g. the 6510's I/O port, <see cref="Cpu6510Port"/>); null for other models.
    /// The machine wires it up (input levels, output-change subscription) at system
    /// construction.
    /// </summary>
    public CpuModelState? ModelState { get; private set; }

    /// <summary>
    /// The 256-entry dispatch table for this CPU's model: one pre-composed handler per
    /// opcode byte (null = undefined byte for the active profile). This is what the
    /// executor runs; the public metadata view of it is <see cref="GetOpCodeInfo"/>.
    /// </summary>
    internal OpCodeDescriptor?[] Descriptors { get; private set; }

    public event EventHandler<CPUInstructionExecutedEventArgs>? InstructionExecuted;
    protected virtual void OnInstructionExecuted(CPUInstructionExecutedEventArgs e)
    {
        var handler = InstructionExecuted;
        handler?.Invoke(this, e);
    }
    public event EventHandler<CPUInstructionToBeExecutedEventArgs>? InstructionToBeExecuted;
    protected virtual void OnInstructionToBeExecuted(CPUInstructionToBeExecutedEventArgs e)
    {
        var handler = InstructionToBeExecuted;
        handler?.Invoke(this, e);
    }

    public event EventHandler<CPUUnknownOpCodeDetectedEventArgs>? UnknownOpCodeDetected;
    protected virtual void OnUnknownOpCodeDetected(CPUUnknownOpCodeDetectedEventArgs e)
    {
        var handler = UnknownOpCodeDetected;
        handler?.Invoke(this, e);
    }

    /// <summary>
    /// Raised when the CPU is about to service a pending hardware NMI, before the
    /// vector at $FFFA/$FFFB is read. Some C64 expansion-port hardware changes
    /// memory mapping as part of NMI acknowledgement, so system integrations can
    /// use this boundary to expose the vector that the real machine would see.
    /// </summary>
    public event EventHandler? NmiAcknowledging;
    protected virtual void OnNmiAcknowledging()
    {
        var handler = NmiAcknowledging;
        handler?.Invoke(this, EventArgs.Empty);
    }

    // Per-instruction event-firing helpers. Each snapshots the delegate first to avoid
    // the standard event-race (subscriber detaches between null check and invocation)
    // and skips the EventArgs allocation when no subscriber is attached -- a measurable
    // win on the full CPU.Execute path where every iteration would otherwise allocate.
    private void RaiseInstructionToBeExecutedIfSubscribed(Memory mem)
    {
        var handler = InstructionToBeExecuted;
        if (handler != null)
            handler(this, new CPUInstructionToBeExecutedEventArgs(this, mem));
    }
    private void RaiseUnknownOpCodeDetectedIfSubscribed(Memory mem, byte opCode)
    {
        var handler = UnknownOpCodeDetected;
        if (handler != null)
            handler(this, new CPUUnknownOpCodeDetectedEventArgs(this, mem, opCode));
    }
    private void RaiseInstructionExecutedIfSubscribed(Memory mem, InstructionExecResult result)
    {
        var handler = InstructionExecuted;
        if (handler != null)
        {
            var instructionExecState = ExecState.ExecStateAfterInstruction(lastinstructionExecutionResult: result);
            handler(this, new CPUInstructionExecutedEventArgs(this, mem, instructionExecState));
        }
    }

    private readonly InstructionExecutor _instructionExecutor;

    private ILogger _logger;

    public CPU() : this(new ExecState(), new NullLoggerFactory(), CpuCompatibilityProfile.ExperimentalUnofficial) { }
    public CPU(ExecState execState) : this(execState, new NullLoggerFactory(), CpuCompatibilityProfile.ExperimentalUnofficial) { }
    public CPU(ILoggerFactory loggerFactory) : this(new ExecState(), loggerFactory, CpuCompatibilityProfile.ExperimentalUnofficial) { }
    public CPU(CpuCompatibilityProfile compatibilityProfile) : this(new ExecState(), new NullLoggerFactory(), compatibilityProfile) { }
    public CPU(ILoggerFactory loggerFactory, CpuCompatibilityProfile compatibilityProfile)
        : this(new ExecState(), loggerFactory, compatibilityProfile) { }

    public CPU(ExecState execState, ILoggerFactory loggerFactory)
        : this(execState, loggerFactory, CpuCompatibilityProfile.ExperimentalUnofficial) { }

    public CPU(ExecState execState, ILoggerFactory loggerFactory, CpuCompatibilityProfile compatibilityProfile)
        : this(execState, loggerFactory, CpuModelIds.Nmos6502, compatibilityProfile) { }

    public CPU(ILoggerFactory loggerFactory, string cpuModelId, CpuCompatibilityProfile compatibilityProfile)
        : this(new ExecState(), loggerFactory, cpuModelId, compatibilityProfile) { }

    /// <summary>
    /// Constructs a CPU with an explicit model (see <see cref="CpuModelIds"/>).
    /// Throws for an unknown model id or a model/profile combination the model does
    /// not support (query <see cref="CpuModelInfo"/> to validate beforehand).
    /// </summary>
    public CPU(ExecState execState, ILoggerFactory loggerFactory, string cpuModelId, CpuCompatibilityProfile compatibilityProfile)
    {
        _logger = loggerFactory.CreateLogger(typeof(CPU).Name);

        ProcessorStatus = new ProcessorStatus();
        ExecState = execState;

        ModelDefinition = CpuModels.GetDefinition(cpuModelId);
        if (!ModelDefinition.SupportedProfiles.Contains(compatibilityProfile))
            throw new DotNet6502Exception($"CPU model '{ModelDefinition.ModelId}' does not support compatibility profile '{compatibilityProfile}'.");
        CompatibilityProfile = compatibilityProfile;
        Descriptors = ModelDefinition.CreateDescriptors(compatibilityProfile);
        ModelState = ModelDefinition.StateFactory?.Invoke();

        // TODO: Inject InstructionExecutor?
        _instructionExecutor = new InstructionExecutor(loggerFactory);
    }

    public CPU Clone()
    {
        return new CPU()
        {
            PC = this.PC,
            SP = this.SP,
            A = this.A,
            X = this.X,
            Y = this.Y,
            ProcessorStatus = this.ProcessorStatus,
            ExecState = this.ExecState.Clone(),
            IsHalted = this.IsHalted,
            CompatibilityProfile = this.CompatibilityProfile,
            ModelDefinition = this.ModelDefinition, // immutable definition, safe to share
            // Shares handler instances with the original. Handlers are stateless.
            Descriptors = this.Descriptors,
            // Copies state values but not event subscribers: the clone must not retain
            // callbacks into the original machine.
            ModelState = this.ModelState?.Clone(),
            _logger = this._logger
        };
    }

    /// <summary>
    /// Executes one instruction with minimal overhead.
    /// Does not fire any events when instruction is executed.
    /// Does not update statistics (ExecState property).
    /// Caller cannot specify any ExecEvaluators.
    /// </summary>
    /// <param name="mem"></param>
    /// <returns></returns>
    public InstructionExecResult ExecuteOneInstructionMinimal(
        Memory mem)
    {
        if (IsHalted)
            return InstructionExecResult.CpuAlreadyHaltedResult(PC);

        var interruptDisableBefore = ProcessorStatus.InterruptDisable;
        _stallCycles = 0;
        var instructionExecutionResult = _instructionExecutor.Execute(this, mem);
        if (_stallCycles > 0)
            instructionExecutionResult = instructionExecutionResult.WithAdditionalCycles(_stallCycles);

        if (!instructionExecutionResult.HaltedCpu)
        {
            RecordInterruptPollPoint(instructionExecutionResult, interruptDisableBefore);

            // Fold the hardware interrupt-entry cost (when one was serviced at this
            // boundary) into this instruction's result, so cycle totals and the
            // system loops that pace devices/frame budgets by it see real elapsed time.
            var interruptCycles = ProcessInterrupts(mem);
            if (interruptCycles > 0)
                instructionExecutionResult = instructionExecutionResult.WithAdditionalCycles(interruptCycles);
        }

        ExecState.UpdateTotal(instructionExecutionResult);

        return instructionExecutionResult;
    }

    /// <summary>
    /// Services any pending hardware interrupts at the current instruction boundary.
    /// Intended for system-level device ticking that occurs after instruction execution.
    /// An interrupt whose device reported an assertion cycle later than the instruction's poll
    /// point (its last cycle) is left pending for the next boundary, as on hardware.
    /// </summary>
    /// <param name="mem"></param>
    /// <returns>
    /// Cycles the interrupt-entry sequence consumed (<see cref="InterruptEntryCycles"/> when an
    /// interrupt was serviced, else 0). Callers that pace devices or frame budgets by cycles
    /// should account for them — the entry sequence is real elapsed time.
    /// </returns>
    public ulong ProcessPendingInterrupts(Memory mem)
    {
        return ProcessInterrupts(mem);
    }

    /// <summary>
    /// Executes one instruction, and will fire events and collect statistics.
    /// </summary>
    /// <param name="mem"></param>
    /// <returns></returns>
    public ExecState ExecuteOneInstruction(
        Memory mem)
    {
        return Execute(mem, LegacyExecEvaluator.OneInstructionExecEvaluator);
    }

    /// <summary>
    /// Executes until BRK instruction is encountered, and will fire events and collect statistics.
    /// </summary>
    /// <param name="mem"></param>
    /// <returns></returns>
    public ExecState ExecuteUntilBRK(
        Memory mem)
    {
        return Execute(mem, LegacyExecEvaluator.UntilBRKExecEvaluator);
    }

    /// <summary>
    /// Executes instructions in a loop until a condition is triggered in one of the specified ExecEvaluators.
    /// Events are also triggered for different stages of the execution.
    /// Statistics are collected.
    /// This can be quite costly performance-wise. See the ExecuteOneInstructionMinimal method for a more performant alternative, but without events, statistics, and ExecEvaluators.
    /// </summary>
    /// <param name="mem"></param>
    /// <param name="execEvaluators"></param>
    /// <returns></returns>
    public ExecState Execute(
        Memory mem,
        params IExecEvaluator[] execEvaluators)
    {
        // Collect stats for this invocation of Execute(). 
        // Whereas the property Cpu.ExecState contains the aggregate stats for all invocations of Execute().
        var thisExecState = new ExecState();

        while (true)
        {
            if (IsHalted)
                break;

            // Evaluate BEFORE executing the next instruction. Checking pre-execution
            // means breakpoints trigger at the correct address (before the instruction
            // at that address runs), and evaluators that count cycles/instructions use
            // the cumulative totals from prior iterations.
            if (AnyEvaluatorTriggered(thisExecState, mem, execEvaluators))
                break;

            RaiseInstructionToBeExecutedIfSubscribed(mem);

            var interruptDisableBefore = ProcessorStatus.InterruptDisable;
            _stallCycles = 0;
            var instructionExecutionResult = _instructionExecutor.Execute(this, mem);
            if (_stallCycles > 0)
                instructionExecutionResult = instructionExecutionResult.WithAdditionalCycles(_stallCycles);

            // Service pending hardware interrupts at this boundary and fold the entry
            // cost into the instruction's result, so both ExecStates, evaluators, the
            // InstructionExecuted event, and callers pacing by cycles see real elapsed
            // time. (NmiAcknowledging consequently fires before InstructionExecuted.)
            if (!instructionExecutionResult.HaltedCpu)
            {
                RecordInterruptPollPoint(instructionExecutionResult, interruptDisableBefore);
                var interruptCycles = ProcessInterrupts(mem);
                if (interruptCycles > 0)
                    instructionExecutionResult = instructionExecutionResult.WithAdditionalCycles(interruptCycles);
            }

            // Aggregate stats directly from the InstructionExecResult into both ExecStates;
            // the previous code path went via ExecStateAfterInstruction() which allocated
            // an ExecState per step.
            ExecState.UpdateTotal(instructionExecutionResult);
            thisExecState.UpdateTotal(instructionExecutionResult);

            if (instructionExecutionResult.HaltedCpu)
            {
                RaiseUnknownOpCodeDetectedIfSubscribed(mem, instructionExecutionResult.OpCodeByte);
                break;
            }

            if (instructionExecutionResult.UnknownInstruction)
            {
                RaiseUnknownOpCodeDetectedIfSubscribed(mem, instructionExecutionResult.OpCodeByte);
                Debug.WriteLine($"Unknown opcode: {instructionExecutionResult.OpCodeByte.ToHex()}");
            }
            else
            {
                RaiseInstructionExecutedIfSubscribed(mem, instructionExecutionResult);
            }
        }

        // Return the per-invocation stats accumulated above.
        return thisExecState;
    }

    private bool AnyEvaluatorTriggered(ExecState thisExecState, Memory mem, IExecEvaluator[] execEvaluators)
    {
        foreach (var execEvaluator in execEvaluators)
        {
            if (execEvaluator.Check(thisExecState, this, mem).Triggered)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Cycles a hardware IRQ/NMI entry sequence consumes on 6502-family CPUs:
    /// two dummy opcode reads, three stack pushes, two vector reads.
    /// </summary>
    public const ulong InterruptEntryCycles = 7;

    private void RecordInterruptPollPoint(InstructionExecResult result, bool interruptDisableBefore)
    {
        var descriptor = Descriptors[result.OpCodeByte];
        var poll = BusCycles > 0 ? BusCycles - 1 : 0;
        // A relative branch taken without a page crossing (3 cycles: not taken is 2, taken across
        // a page is 4) polls interrupts only at the end of its first cycle. The descriptor table
        // decides what is a branch for this model ($80 is BRA on the 65C02, a NOP on the NMOS 6502).
        if (result.CyclesConsumed == 3 && poll > 0 && descriptor?.Addressing == AddrMode.Relative)
            poll--;
        _interruptPollBusCycle = poll;
        // CLI, SEI and PLP (per the model's table) change the I flag after the poll.
        _interruptDisableAtPoll = descriptor?.ChangesInterruptDisableAfterPoll == true ? interruptDisableBefore : null;
    }

    private void RecordInterruptPollPointAfterInterruptEntry()
    {
        // The entry sequence behaves like an instruction: lines are polled again at its end, and
        // the I flag it set is what the next decision sees.
        _interruptPollBusCycle = BusCycles > 0 ? BusCycles - 1 : 0;
        _interruptDisableAtPoll = null;
    }

    /// <returns>Cycles consumed: <see cref="InterruptEntryCycles"/> if an interrupt was serviced, else 0.</returns>
    private ulong ProcessInterrupts(Memory mem)
    {
        if (IsHalted)
            return 0;

        _stallCycles = 0;   // the entry sequence's reads can be stalled too; report those cycles with it
        if (CPUInterrupts.NMIPending && CPUInterrupts.NMIPendingAtBusCycle <= _interruptPollBusCycle)
        {
            OnNmiAcknowledging();
            // The vector is read exactly once, inside ProcessHardwareNMI (as on real hardware,
            // where the reads can hit mapped handlers). After entry PC holds the vector target,
            // so the gated log line below reuses it instead of a second real vector read.
            var logNmiDebug = _logger.IsEnabled(LogLevel.Debug);
            ushort pcBeforeNmi = PC;
            string? nmiSources = logNmiDebug ? string.Join(",", CPUInterrupts.ActiveNMISources) : null;
            CPUInterrupts.ClearPendingNMI();
            ProcessHardwareNMI(mem);
            if (logNmiDebug)
            {
                ushort nmiVector = PC;
                _logger.LogDebug(
                    "Servicing NMI. PC={PcBeforeNmi:X4}, Vector={NmiVector:X4}, ActiveSources=[{NmiSources}]",
                    pcBeforeNmi,
                    nmiVector,
                    nmiSources);
            }
            RecordInterruptPollPointAfterInterruptEntry();
            return InterruptEntryCycles + _stallCycles;
        }

        if (CPUInterrupts.IRQLineEnabled
            && CPUInterrupts.IRQAssertedAtBusCycle <= _interruptPollBusCycle
            && !(_interruptDisableAtPoll ?? ProcessorStatus.InterruptDisable))
        {
            // Sources raised with autoAcknowledge are dropped now; manually acknowledged
            // sources keep the line asserted until their device clears them.
            CPUInterrupts.AcknowledgeAutoAcknowledgingIRQSources();
            ProcessHardwareIRQ(mem);
            RecordInterruptPollPointAfterInterruptEntry();
            return InterruptEntryCycles + _stallCycles;
        }

        return 0;
    }

    /// <summary>
    /// Generate a IRQ.
    /// Ref: https://www.pagetable.com/?p=410
    /// </summary>
    /// <param name="mem"></param>
    private void ProcessHardwareIRQ(Memory mem)
    {
        // Real hardware entry: the first two cycles of the 7-cycle sequence read the
        // next opcode byte (twice, discarded) while the interrupt takes over the
        // instruction flow. Observable through mapped I/O at the PC address.
        FetchByte(mem, PC);
        FetchByte(mem, PC);

        // The return address pushed to stack is the current PC (the address of the next instruction at this point)
        ushort pcPushedToStack = PC;
        PushWordToStack(pcPushedToStack, mem);
        // Set the Break flag on the copy of the ProcessorStatus that will be stored in stack.
        var processorStatusCopy = ProcessorStatus;
        processorStatusCopy.Break = false;      // Break flag should be cleared on the PS value stored on stack, so that the IRQ routine can determine if the IRQ was generated by hardware, or the BRK instruction.
        processorStatusCopy.Unused = true;
        PushByteToStack(processorStatusCopy.Value, mem);
        // Set current Interrupt flag
        ProcessorStatus.InterruptDisable = true;
        // Model policy (per event, not hot path): CMOS parts clear Decimal on interrupt
        // entry, after the status byte (with D intact) was pushed. NMOS leaves D as-is.
        if (ModelDefinition.Traits.ClearsDecimalOnInterrupt)
            ProcessorStatus.Decimal = false;
        // Change PC to address found at BRK/IRQ handler vector
        PC = FetchWord(mem, CPU.BrkIRQHandlerVector);
    }

    /// <summary>
    /// Generate a Non-maskable Interrupt.
    /// Ref: https://www.pagetable.com/?p=410
    /// </summary>
    /// <param name="mem"></param>
    private void ProcessHardwareNMI(Memory mem)
    {
        // Real hardware entry: the first two cycles of the 7-cycle sequence read the
        // next opcode byte (twice, discarded) while the interrupt takes over the
        // instruction flow. Observable through mapped I/O at the PC address.
        FetchByte(mem, PC);
        FetchByte(mem, PC);

        // The return address pushed to stack is the current PC (the address of the next instruction at this point)
        ushort pcPushedToStack = PC;
        PushWordToStack(pcPushedToStack, mem);
        // Set the Break flag on the copy of the ProcessorStatus that will be stored in stack.
        var processorStatusCopy = ProcessorStatus;
        processorStatusCopy.Break = false;      // Break flag should be cleared on the PS value stored on stack, so that the IRQ routine can determine if the IRQ was generated by hardware, or the BRK instruction.
        processorStatusCopy.Unused = true;
        PushByteToStack(processorStatusCopy.Value, mem);
        // Set current Interrupt flag
        ProcessorStatus.InterruptDisable = true;
        // Model policy (per event, not hot path): CMOS parts clear Decimal on interrupt
        // entry, after the status byte (with D intact) was pushed. NMOS leaves D as-is.
        if (ModelDefinition.Traits.ClearsDecimalOnInterrupt)
            ProcessorStatus.Decimal = false;
        // Change PC to address found at BRK/IRQ handler vector
        PC = FetchWord(mem, CPU.NonMaskableIRQHandlerVector);
    }

    /// <summary>
    /// Issue a Reset
    /// </summary>
    /// <param name="mem"></param>
    public void Reset(Memory mem)
    {
        // Model policy: CMOS parts clear Decimal on reset; the emulator's NMOS reset
        // deliberately touches no flags (see CpuNmosCharacterizationTests).
        if (ModelDefinition.Traits.ClearsDecimalOnInterrupt)
            ProcessorStatus.Decimal = false;
        // Change PC to address found at BRK/IRQ handler vector
        PC = FetchWord(mem, CPU.ResetVector);
        _interruptPollBusCycle = BusCycles;
        _interruptDisableAtPoll = null;
        IsHalted = false;
    }

    internal void Halt()
    {
        IsHalted = true;
    }

    /// <summary>
    /// The stable id of the CPU model this CPU was constructed with
    /// (see <see cref="CpuModelIds"/>).
    /// </summary>
    public string CpuModelId => ModelDefinition.ModelId;

    /// <summary>
    /// True if the opcode byte is a defined instruction for THIS CPU's model and
    /// compatibility profile. Model-aware — on a 65C02 every byte is defined.
    /// </summary>
    public bool IsOpCodeDefined(byte opCode) => Descriptors[opCode] is not null;

    /// <summary>
    /// Instruction size in bytes for the opcode byte, per THIS CPU's model
    /// (e.g. $9C is 1 byte/undefined on NMOS profiles but 3 bytes STZ abs on a 65C02).
    /// Undefined opcodes report size 1.
    /// </summary>
    public byte GetOpCodeSize(byte opCode) => Descriptors[opCode]?.Size ?? 1;

    /// <summary>
    /// Model-correct metadata for the opcode byte, per THIS CPU's model and
    /// compatibility profile (e.g. $9C reports STZ abs on a 65C02 but the undocumented
    /// SHY abs,X on NMOS models). Null when the byte is not a defined instruction for
    /// this model/profile.
    /// </summary>
    public OpCodeInfo? GetOpCodeInfo(byte opCode)
    {
        var descriptor = Descriptors[opCode];
        if (descriptor is null)
            return null;
        return new OpCodeInfo
        {
            Code = descriptor.Code,
            Mnemonic = descriptor.Mnemonic,
            AddressingMode = descriptor.Addressing,
            Size = descriptor.Size,
            MinimumCycles = descriptor.BaseCycles,
            Documented = descriptor.Documented,
        };
    }

    /// <summary>
    /// Gets the Zero Page address at the current PC with Y offset.
    /// If specified, make sure calculated address wraps around after 0xff.
    /// </summary>
    /// <param name="zeroPageAddress"></param>
    /// <param name="wrapZeroPage"></param>
    /// <returns></returns>
    public ushort CalcZeroPageAddressX(byte zeroPageAddress, bool wrapZeroPage = true)
    {
        var zeroPageAddressX = (ushort)(zeroPageAddress + X);

        // Wrap around when Zero Page Address + X is greater than one byte (0xff)
        if (wrapZeroPage)
            zeroPageAddressX = (ushort)(zeroPageAddressX & 0xff);

        return zeroPageAddressX;
    }

    /// <summary>
    /// Gets the Zero Page address at the current PC with Y offset.
    /// If specified, make sure calculated address wraps around after 0xff.
    /// </summary>
    /// <param name="zeroPageAddress"></param>
    /// <param name="wrapZeroPage"></param>
    /// <returns></returns>
    public ushort CalcZeroPageAddressY(byte zeroPageAddress, bool wrapZeroPage = true)
    {
        var zeroPageAddressY = (ushort)(zeroPageAddress + Y);

        // Wrap around when Zero Page Address + Y is greater than one byte (0xff)
        if (wrapZeroPage)
            zeroPageAddressY = (ushort)(zeroPageAddressY & 0xff);

        return zeroPageAddressY;
    }

    /// <summary>
    /// Get instruction opcode from the byte on current PC (Program Counter).
    /// Increase PC by 1.
    /// </summary>
    /// <param name="mem"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte FetchInstruction(Memory mem)
    {
        var data = FetchByte(mem, PC);
        PC++;
        return data;
    }

    /// <summary>
    /// Get instruction operand from the byte on current PC (Program Counter).
    /// Increase PC by 1. Semantically identical to <see cref="FetchInstruction"/>;
    /// kept as a separate named entry point so caller intent is readable. The
    /// AggressiveInlining hint ensures the JIT folds both into their callers,
    /// so there is no extra call cost on the per-instruction hot path.
    /// </summary>
    /// <param name="mem"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte FetchOperand(Memory mem) => FetchInstruction(mem);

    /// <summary>
    /// Gets the 16-bit word at current PC (Program Counter), adjusted for little endian.
    /// Increase PC by 2.
    /// </summary>
    /// <param name="mem"></param>
    /// <returns></returns>
    public ushort FetchOperandWord(Memory mem)
    {
        var fullAddress = FetchWord(mem, PC);
        PC += 2;
        return fullAddress;
    }

    /// <summary>
    /// Get a byte from specified address.
    /// Consume 1 cycle.
    /// </summary>
    /// <param name="mem"></param>
    /// <param name="address"></param>
    /// <returns></returns>
    public byte FetchByte(Memory mem, ushort address)
    {
        BusCycles++;
        if (BusCycles >= _readStallCheckFromBusCycle)
            StallRead();
        return mem.FetchByte(address);
    }

    // A bus master holds the CPU: the read happens once the bus is released, and the cycles in
    // between are time without accesses.
    private void StallRead()
    {
        var stall = _busStallSource!.StallCyclesForRead(BusCycles, out _readStallCheckFromBusCycle);
        if (stall == 0)
            return;
        BusCycles += stall;
        _stallCycles += stall;
    }

    /// <summary>
    /// Get a word from specified address, adjusted for little endian.
    /// Consume 2 cycles.
    /// </summary>
    /// <param name="mem"></param>
    /// <param name="address"></param>
    /// <returns></returns>
    public ushort FetchWord(Memory mem, ushort address)
    {
        // Two bus cycles, low byte first; the high byte address wraps at $FFFF like the helper did.
        var lowByte = FetchByte(mem, address);
        var highByte = FetchByte(mem, (ushort)(address + 1));
        return ByteHelpers.ToLittleEndianWord(lowByte, highByte);
    }

    /// <summary>
    /// Get a byte from the current SP (Stack Pointer) + 1 (as current position is the current free one)
    /// Increases SP with 1 after reading.
    /// Consume 1 cycle.
    /// </summary>
    /// <param name="mem"></param>
    /// <param name="SP"></param>
    /// <returns></returns>
    public byte PopByteFromStack(Memory mem)
    {
        // Calculate absolute address for Stack Pointer.
        // Memory locations 0x0100-0x01ff.  SP is relative to 0x0100 and decreases for every value put on the stack.
        // As SP currently points to the next free position, well go back one byte where the previous data was stored.
        // We will read one bytes from that position (SP+1), and later below update the SP to SP+1 (as that is now the next free position)
        ushort address = (ushort)(StackBaseAddress + (byte)(SP + 1));
        byte data = FetchByte(mem, address);

        // Move SP back to latest stored byte (the current SP position always points to the currently free position)
        SP++;

        // As we now read the SP+1 position, it's free for next use.
        return data;
    }

    /// <summary>
    /// Get a word (adjusted for little endian) from the current SP (Stack Pointer) + 2 (as current position is the current free one)
    /// Increases SP by 2 after reading.
    /// Consume 2 cycles
    /// </summary>
    /// <param name="mem"></param>
    /// <returns></returns>
    public ushort PopWordFromStack(Memory mem)
    {
        byte lowByte = PopByteFromStack(mem);   // lowbyte is read first
        byte highByte = PopByteFromStack(mem);   // highbyte is read second
        return ByteHelpers.ToLittleEndianWord(lowByte, highByte);

    }

    /// <summary>
    /// Push one byte to Stack at current SP (Stack Pointer).
    /// Current SP points to the next free location to push data to.
    /// Decreases SP by 1.
    /// </summary>
    /// <param name="byteData"></param>
    /// <param name="mem"></param>
    public void PushByteToStack(byte byteData, Memory mem)
    {
        // Calculate absolute address for Stack Pointer.
        // Memory locations 0x0100-0x01ff.  SP is relative to 0x0100 and decreases for every value put on the stack.
        ushort address = (ushort)(StackBaseAddress + SP);
        StoreByte(byteData, mem, address);

        // Update Stack Pointer so it points to next free location
        SP -= 1;
    }

    /// <summary>
    /// Push one word (adjusted for little endian).
    /// Decreases SP by 2.
    /// The highbyte of address is pushed first, then the lowbyte  (so when it's read back again it will be read as normal with lowbyte first).
    /// </summary>
    /// <param name="word"></param>
    /// <param name="mem"></param>
    public void PushWordToStack(ushort word, Memory mem)
    {
        PushByteToStack(word.Highbyte(), mem);
        PushByteToStack(word.Lowbyte(), mem);
    }

    /// <summary>
    /// Gets the full 16-bit address at current PC, with X offset.
    /// If the page boundary was crossed, the out parameter didCrossPageBoundary is set to true.
    /// </summary>
    /// <param name="fullAddress"></param>
    /// <param name="didCrossPageBoundary"></param>
    /// <returns></returns>
    public ushort CalcFullAddressX(ushort fullAddress, out bool didCrossPageBoundary)
    {
        didCrossPageBoundary = (fullAddress & 0x00ff) + X > 0xff;
        var fullAddressX = (ushort)(fullAddress + X);
        return fullAddressX;
    }

    /// <summary>
    /// Gets the full 16-bit address at current PC, with Y offset.
    /// If the page boundary was crossed, the out parameter didCrossPageBoundary is set to true.
    /// </summary>
    /// <param name="fullAddress"></param>
    /// <param name="didCrossPageBoundary"></param>
    /// <returns></returns>
    public ushort CalcFullAddressY(ushort fullAddress, out bool didCrossPageBoundary)
    {
        didCrossPageBoundary = (fullAddress & 0x00ff) + Y > 0xff;
        var fullAddressY = (ushort)(fullAddress + Y);
        return fullAddressY;
    }

    /// <summary>
    /// Stores one byte in memory.
    /// Consume 1 cycle.
    /// </summary>
    /// <param name="byteData"></param>
    /// <param name="mem"></param>
    /// <param name="address"></param>
    public void StoreByte(byte byteData, Memory mem, ushort address)
    {
        BusCycles++;
        mem.WriteByte(address, byteData);
    }

    /// <summary>
    /// Stores one word in memory (adjusted for little endian).
    /// Consume 2 cycles.
    /// </summary>
    /// <param name="word"></param>
    /// <param name="mem"></param>
    /// <param name="address"></param>
    public void StoreWord(ushort word, Memory mem, ushort address)
    {
        StoreByte(word.Lowbyte(), mem, address);
        StoreByte(word.Highbyte(), mem, (ushort)(address + 1));
    }
}
