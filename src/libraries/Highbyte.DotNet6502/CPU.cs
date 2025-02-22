using System.Diagnostics;
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

    public bool NMI => CPUInterrupts.NMILineEnabled;

    /// <summary>
    /// Aggregated stats and info for all invocations of Execute()
    /// </summary>
    /// <value></value>
    public ExecState ExecState { get; private set; }

    public InstructionList InstructionList { get; private set; }

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

    private readonly InstructionExecutor _instructionExecutor;

    private ILogger _logger;

    public CPU() : this(new ExecState(), new NullLoggerFactory()) { }
    public CPU(ExecState execState) : this(execState, new NullLoggerFactory()) { }
    public CPU(ILoggerFactory loggerFactory) : this(new ExecState(), loggerFactory) { }

    public CPU(ExecState execState, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(typeof(CPU).Name);

        ProcessorStatus = new ProcessorStatus();
        ExecState = execState;
        // TODO: Inject instruction list?
        InstructionList = InstructionList.GetAllInstructions();
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
            InstructionList = this.InstructionList.Clone(),
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
        var instructionExecutionResult = _instructionExecutor.Execute(this, mem);

        ExecState.UpdateTotal(instructionExecutionResult);

        ProcessInterrupts(mem);

        return instructionExecutionResult;
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

        bool doNextInstruction = true;
        while (doNextInstruction)
        {
            // Fire event before instruction executes
            OnInstructionToBeExecuted(new CPUInstructionToBeExecutedEventArgs(this, mem));

            // Execute instruction
            var instructionExecutionResult = _instructionExecutor.Execute(this, mem);

            // Collect stats for this instruction.
            // Whereas the property thisExecState contains the aggregate stats for this invocation of Execute().
            // and the property Cpu.ExecState contains the aggregate stats for all invocations of Execute().
            var instructionExecState = ExecState.ExecStateAfterInstruction(
                lastinstructionExecutionResult: instructionExecutionResult);

            // Update/Aggregate total Cpu.ExecState stats
            ExecState.UpdateTotal(instructionExecState);
            // Update/Aggregate this invocation of Execute() ExecState stats
            thisExecState.UpdateTotal(instructionExecState);

            if (instructionExecutionResult.UnknownInstruction)
            {
                // Fire event for unknown instruction
                OnUnknownOpCodeDetected(new CPUUnknownOpCodeDetectedEventArgs(this, mem, instructionExecutionResult.OpCodeByte));
                Debug.WriteLine($"Unknown opcode: {instructionExecutionResult.OpCodeByte.ToHex()}");
            }
            else
            {
                // Fire event for instruction recognized and executed
                OnInstructionExecuted(new CPUInstructionExecutedEventArgs(this, mem, instructionExecState));
            }

            ProcessInterrupts(mem);

            // Evaluate if execution shall continue to next instruction, or stop here.
            // Will continue only if all of the ExecEvaluators reports true.
            foreach (var execEvaluator in execEvaluators)
            {
                var execEvaluatorTriggerResult = execEvaluator.Check(thisExecState, this, mem);
                if (execEvaluatorTriggerResult.Triggered)
                {
                    doNextInstruction = false;
                    break;
                }
            }
        }

        // Return stats for this invocation of Execute();
        return thisExecState;
    }

    private void ProcessInterrupts(Memory mem)
    {
        if (CPUInterrupts.NMILineEnabled)
        {
            // TODO: Should all NMI sources be cleared here?
            for (int i = CPUInterrupts.ActiveNMISources.Count - 1; i >= 0; i--)
            {
                var source = CPUInterrupts.ActiveNMISources.ElementAt(i);
                CPUInterrupts.SetNMISourceInactive(source);
            }
            ProcessHardwareNMI(mem);
        }
        else if (CPUInterrupts.IRQLineEnabled && !ProcessorStatus.InterruptDisable)
        {
            for (int i = CPUInterrupts.ActiveIRQSources.Count - 1; i >= 0; i--)
            {
                var source = CPUInterrupts.ActiveIRQSources.ElementAt(i).Key;
                // Automatically remove IRQ sources that should not manually be acknowledged.
                if (CPUInterrupts.ActiveIRQSources[source])
                {
                    CPUInterrupts.SetIRQSourceInactive(source);
                }
            }
            ProcessHardwareIRQ(mem);
        }
    }

    /// <summary>
    /// Generate a IRQ.
    /// Ref: https://www.pagetable.com/?p=410
    /// </summary>
    /// <param name="mem"></param>
    private void ProcessHardwareIRQ(Memory mem)
    {
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
        // Change PC to address found at BRK/IRQ handler vector
        PC = FetchWord(mem, CPU.NonMaskableIRQHandlerVector);
    }

    /// <summary>
    /// Issue a Reset
    /// </summary>
    /// <param name="mem"></param>
    public void Reset(Memory mem)
    {
        // Change PC to address found at BRK/IRQ handler vector
        PC = FetchWord(mem, CPU.ResetVector);
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
    public byte FetchInstruction(Memory mem)
    {
        var data = FetchByte(mem, PC);
        PC++;
        return data;
    }

    /// <summary>
    /// Get instruction operand from the byte on current PC (Program Counter).
    /// Increase PC by 1.
    /// </summary>
    /// <param name="mem"></param>
    /// <returns></returns>
    public byte FetchOperand(Memory mem)
    {
        var data = FetchByte(mem, PC);
        PC++;
        return data;
    }

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
        byte data = mem.FetchByte(address);
        return data;
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
        ushort data = mem.FetchWord(address);
        return data;
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
        //var addrFromStack = new byte[2];
        //addrFromStack[0] = PopByteFromStack(mem);   // lowbyte is read first
        //addrFromStack[1] = PopByteFromStack(mem);   // highbyte is read second
        //return ByteHelpers.ToLittleEndianWord(addrFromStack);

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
        mem.WriteWord(address, word);
    }
}
