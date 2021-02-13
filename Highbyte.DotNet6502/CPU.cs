using System;
using System.Diagnostics;

namespace Highbyte.DotNet6502
{
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
        ///  Address for vector to BRK/interrupt request handler
        /// </summary>
        public const ushort BrkIRQHandlerVector = 0xfffe; // + 0xffff

        public ExecState ExecState {get; private set;}

        public InstructionList InstructionList {get; private set;}

        public event EventHandler<CPUInstructionExecutedEventArgs> InstructionExecuted;
        protected virtual void OnInstructionExecuted(CPUInstructionExecutedEventArgs e)
        {
            var handler = InstructionExecuted;
            handler?.Invoke(this, e);
        }
        public event EventHandler<CPUInstructionToBeExecutedEventArgs> InstructionToBeExecuted;
        protected virtual void OnInstructionToBeExecuted(CPUInstructionToBeExecutedEventArgs e)
        {
            var handler = InstructionToBeExecuted;
            handler?.Invoke(this, e);
        }


        public event EventHandler<CPUUnknownOpCodeDetectedEventArgs> UnknownOpCodeDetected;
        protected virtual void OnUnknownOpCodeDetected(CPUUnknownOpCodeDetectedEventArgs e)
        {
            var handler = UnknownOpCodeDetected;
            handler?.Invoke(this, e);
        }        

        private readonly InstructionExecutor _instructionExecutor;

        public CPU()
        {
            ProcessorStatus = new ProcessorStatus();
            ExecState = new ExecState();
            // TODO: Inject instruction list?
            InstructionList = InstructionList.GetAllInstructions();

            // TOD: Inject InstructionExecutor?
            _instructionExecutor = new InstructionExecutor();
        }

        public CPU Clone()
        {
            return new CPU 
            {
                PC = this.PC,
                SP = this.SP,
                A = this.A,
                X = this.X,
                Y = this.Y,
                ProcessorStatus = this.ProcessorStatus.Clone(),
                ExecState = this.ExecState.Clone(),
                InstructionList = this.InstructionList.Clone(),
            };
        }

        public ulong Execute(
            Memory mem, 
            ExecOptions execOptions)
        {
            // Get current cycle count
            ulong startCycleCount = ExecState.CyclesConsumed;
            bool doNextInstruction = true;
            ulong cyclesConsumedThisIteration = 0;

            while(doNextInstruction)
            {
                // Fire event before instruction executes
                OnInstructionToBeExecuted(new CPUInstructionToBeExecutedEventArgs(this, mem));

                // Execute instruction
                ushort PCBeforeInstructionExecuted = PC;
                var cycleCountBeforeInstruction = ExecState.CyclesConsumed;
                var instructionExecutionResult = _instructionExecutor.Execute(this, mem);

                // Update CPU state with some statistics
                ExecState.CyclesConsumed = cycleCountBeforeInstruction + instructionExecutionResult.CyclesConsumed ;
                ExecState.LastOpCode = instructionExecutionResult.OpCodeByte;
                ExecState.LastOpCodeWasHandled = !instructionExecutionResult.UnknownInstruction;
                if(instructionExecutionResult.UnknownInstruction)
                    ExecState.UnknownOpCodeCount++;
                ExecState.PCBeforeLastOpCodeExecuted = PCBeforeInstructionExecuted;
                ExecState.InstructionsExecutionCount++;

                // Fire "unknown opcode" or "instruction executed" event
                if(instructionExecutionResult.UnknownInstruction)
                {
                    OnUnknownOpCodeDetected(new CPUUnknownOpCodeDetectedEventArgs(this, mem, instructionExecutionResult.OpCodeByte));
                    Debug.WriteLine($"Unknown opcode: {instructionExecutionResult.OpCodeByte.ToHex()}");

                    // Check if we're configured to throw exception when unknown exception occurs
                    if(execOptions.UnknownInstructionThrowsException)
                        throw new DotNet6502Exception($"Unknown opcode: {instructionExecutionResult.OpCodeByte.ToHex()}"); 
                }
                else
                {
                    OnInstructionExecuted(new CPUInstructionExecutedEventArgs(this, mem));
                }
            
                // How many cycles have we consumed in this call to cpu.Execute()?
                cyclesConsumedThisIteration = ExecState.CyclesConsumed - startCycleCount;

                // Check if we should continue executing instructions
                if(execOptions.CyclesRequested.HasValue  && cyclesConsumedThisIteration >= execOptions.CyclesRequested.Value)
                    doNextInstruction = false;
                if(execOptions.MaxNumberOfInstructions.HasValue && ExecState.InstructionsExecutionCount >= execOptions.MaxNumberOfInstructions.Value) 
                    doNextInstruction = false;
                if(!instructionExecutionResult.UnknownInstruction && execOptions.ExecuteUntilInstruction.HasValue && instructionExecutionResult.OpCodeByte == execOptions.ExecuteUntilInstruction.Value.ToByte())
                     doNextInstruction = false;
                if(execOptions.ExecuteUntilInstructions.Count > 0 && execOptions.ExecuteUntilInstructions.Contains(instructionExecutionResult.OpCodeByte))
                     doNextInstruction = false;
                if(execOptions.ExecuteUntilPC.HasValue && PC == execOptions.ExecuteUntilPC.Value)
                    doNextInstruction = false;
                if(execOptions.ExecuteUntilExecutedInstructionAtPC.HasValue && PCBeforeInstructionExecuted == execOptions.ExecuteUntilExecutedInstructionAtPC.Value)
                    doNextInstruction = false;                  
            }

            // Return cycles consumed in this call to Execute (for total, inspect ExecState.TotalCyclesConsumed)
            return cyclesConsumedThisIteration;
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
            if(wrapZeroPage)
                zeroPageAddressX = (ushort)(zeroPageAddressX & 0xff); 

            // Is the extra cycle because of the calculation of zp address + X ?
            ExecState.CyclesConsumed++;

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
            if(wrapZeroPage)
                zeroPageAddressY = (ushort)(zeroPageAddressY & 0xff); 

            // Is the extra cycle because of the calculation of zp address + X ?
            ExecState.CyclesConsumed++;

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
            PC ++;
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
            PC ++;
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
            ExecState.CyclesConsumed++;
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
            ExecState.CyclesConsumed +=2;
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
            ushort address = (ushort) (StackBaseAddress + (byte)(SP + 1)); 
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
            // Calculate absolute address for Stack Pointer.
            // Memory locations 0x0100-0x01ff. SP is relative to 0x0100 and decreases for every value put on the stack.
            // As SP currently points to the next free position, well go back one byte where the previous data was stored.
            // We will read two bytes from that position (SP+1), and later below update the SP to SP+2 (as that is now the next free position)
            ushort address = (ushort) (StackBaseAddress + (byte)(SP + 1));    
            ushort data = FetchWord(mem, address);

            // Update Stack Pointer
            SP +=2;

            return data;
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
            ushort address = (ushort) (StackBaseAddress + SP);    
            StoreByte(byteData, mem, address);

            // Update Stack Pointer so it points to next free location
            SP -= 1;
        }

        /// <summary>
        /// Push one word (adjusted for little endian) to Stack at current SP (Stack Pointer) - 1.
        /// Current SP points to the next free location to push data to (therefore we need to store it at -1 to have room for a word).
        /// Decreases SP by 2.
        /// </summary>
        /// <param name="word"></param>
        /// <param name="mem"></param>
        public void PushWordToStack(ushort word, Memory mem)
        {
            // Calculate absolute address for Stack Pointer.
            // Memory locations 0x0100-0x01ff.  SP is relative to 0x0100 and decreases for every value put on the stack.
            // As we will write a word we must modify the address by -1. Also below, we then update the SP by -2 (to point to the first free location again)
            ushort address = (ushort) (StackBaseAddress + (byte)(SP - 1));
            StoreWord(word, mem, address);

            // Update SP (Stack Pointer). 
            // The SP points to the position we can push a new value to.
            // As need to write a word, we'll need to move the stack pointer back two bytes (as we currently wrote our word to positions SP-1 (lowbyte) and SP (highbyte))
            SP -= 2; 
        }

        /// <summary>
        /// Gets the full 16-bit address at current PC, with X offset.
        /// Takes extra cycle if final address cross page boundary.
        /// </summary>
        /// <param name="fullAddress"></param>
        /// <returns></returns>
        private ushort CalcFullAddressX(ushort fullAddress)
        {
            return CalcFullAddressX(fullAddress, out _, alwaysExtraCycleWhenCrossBoundary: false);
        }

        /// <summary>
        /// Gets the full 16-bit address at current PC, with X offset.
        /// Takes extra cycle if final address cross page boundary.
        /// If alwaysExtraCycleWhenCrossBoundary is set to true, the extra cycle is always added.
        /// </summary>
        /// <param name="fullAddress"></param>
        /// <returns></returns>
        public ushort CalcFullAddressX(ushort fullAddress, bool alwaysExtraCycleWhenCrossBoundary)
        {
            return CalcFullAddressX(fullAddress, out _, alwaysExtraCycleWhenCrossBoundary);
        }

        /// <summary>
        /// Gets the full 16-bit address at current PC, with X offset.
        /// Takes extra cycle if final address cross page boundary.
        /// If alwaysExtraCycleWhenCrossBoundary is set to true, the extra cycle is always added.
        /// If the page boundary was crossed, the out paraamter didCrossPageBoundary is set to true.
        /// </summary>
        /// <param name="fullAddress"></param>
        /// <param name="alwaysExtraCycleWhenCrossBoundary"></param>
        /// <returns></returns>
        public ushort CalcFullAddressX(ushort fullAddress, out bool didCrossPageBoundary, bool alwaysExtraCycleWhenCrossBoundary)
        {
            didCrossPageBoundary = (fullAddress & 0x00ff) + X > 0xff;

            // Check if adding X to address will cross page boundary. If so, one more cycle is consumed
            if( alwaysExtraCycleWhenCrossBoundary || didCrossPageBoundary)
                ExecState.CyclesConsumed++;

            var fullAddressX = (ushort)(fullAddress + X);
            return fullAddressX;
        }

        /// <summary>
        /// Gets the full 16-bit address at current PC, with Y offset.
        /// Takes extra cycle if final address cross page boundary.
        /// </summary>
        /// <param name="fullAddress"></param>
        /// <param name="alwaysExtraCycleWhenCrossBoundary"></param>
        /// <returns></returns>
        public ushort CalcFullAddressY(ushort fullAddress, bool alwaysExtraCycleWhenCrossBoundary = false)
        {
            return CalcFullAddressY(fullAddress, out bool _, alwaysExtraCycleWhenCrossBoundary);
        }

        /// <summary>
        /// Gets the full 16-bit address at current PC, with Y offset.
        /// Takes extra cycle if final address cross page boundary.
        /// </summary>
        /// <param name="fullAddress"></param>
        /// <param name="didCrossPageBoundary"></param>
        /// <param name="alwaysExtraCycleWhenCrossBoundary"></param>
        /// <returns></returns>
        public ushort CalcFullAddressY(ushort fullAddress, out bool didCrossPageBoundary, bool alwaysExtraCycleWhenCrossBoundary)
        {
            didCrossPageBoundary = (fullAddress & 0x00ff) + Y > 0xff;

            // Check if adding X to address will cross page boundary. If so, one more cycle is consumed
            if( alwaysExtraCycleWhenCrossBoundary || didCrossPageBoundary)
                ExecState.CyclesConsumed++;

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
            // Consume 1 cycle for writing a 8 bit byte
            ExecState.CyclesConsumed++;
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
            ExecState.CyclesConsumed += 2; // Consume 2 cycles for writing a 16 bit word
        }
    }
}