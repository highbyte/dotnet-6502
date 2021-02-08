using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Force Interrupt
    /// The BRK instruction forces the generation of an interrupt request.
    /// The program counter and processor status are pushed on the stack then the IRQ interrupt vector at $FFFE/F 
    /// is loaded into the PC and the break flag in the status set to one.
    /// </summary>
    public class BRK : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            // BRK is strange. The complete instruction is only one byte but the processor increases 
            // the return address pushed to stack is the *second* byte after the opcode!
            // It is advisable to use a NOP after it to avoid issues (when returning from BRK with RTI, the PC will point to the next-next instruction)
            ushort pcPushedToStack = cpu.PC;
            pcPushedToStack++;
            cpu.PushWordToStack(pcPushedToStack, mem);
            // Set the Break flag on the copy of the ProcessorStatus that will be stored in stack.
            var processorStatusCopy = cpu.ProcessorStatus.Clone();
            processorStatusCopy.Break = true;
            processorStatusCopy.Unused = true;
            cpu.PushByteToStack(processorStatusCopy.Value, mem);
            // TODO: Consume extra cycles to change SP? Why not as many extra as RTI?
            cpu.ExecState.CyclesConsumed++;
            // BRK sets current Interrupt flag
            cpu.ProcessorStatus.InterruptDisable = true;
            // Change PC to address found at BRK/IEQ handler vector
            cpu.PC = cpu.FetchWord(mem, CPU.BrkIRQHandlerVector);            

            return true;
        }

        public BRK()
        {
            _opCodes = new List<OpCode>
            {
                new OpCode
                {

                    Code = Ins.BRK,
                    AddressingMode = AddrMode.Implied,
                    Size = 1,
                    Cycles = 7
                },
            };
        }
    }
}
