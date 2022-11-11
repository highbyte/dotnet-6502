using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class BRK_test
    {
        [Fact]
        public void BRK_Takes_Takes_7_Cycles()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.BRK,
                ExpectedCycles = 7,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void BRK_Sets_Interrupt_Flag_And_Nothing_Else()
        {
            var test = new TestSpec()
            {
                C              = false, // Status bit 0
                Z              = true,
                I              = false, // Status bit 4 (Interrupt) clear when we start
                D              = true,
                B              = false, 
                U              = false,
                V              = false,
                N              = true,  // Status bit 7
                OpCode         = OpCodeId.BRK,
                ExpectedC      = false, // Unchanged.
                ExpectedZ      = true,  // Unchanged
                ExpectedI      = true,  // Status bit 4 (Interrupt) set after instruction
                ExpectedD      = true,  // Unchanged
                ExpectedB      = false, // B flag should only be set on the copy of the status register that was pushed to stack
                ExpectedU      = false, // U flag should only be set on the copy of the status register that was pushed to stack
                ExpectedV      = false, // Unchanged
                ExpectedN      = true,  // Unchanged
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void BRK_Pushes_PC_To_Stack()
        {
            var test = new TestSpec()
            {
                PC             = 0x2000,
                PS             = 0b10001011,
                SP             = 0xff,
                OpCode         = OpCodeId.BRK,
                ExpectedSP     = 0xff - 3,      // 2 bytes for PC + 1 byte for PS
            };
            var mem = test.TestContext.Mem;

            // Execute and verify instruction according to TestSpec above
            test.Execute_And_Verify(AddrMode.Implied);

            // Do additional testing not covered by TestSpec class.

            // Remember that stack works downwards (0xff-0x00), points to the next free location, and is located at address 0x0100 + SP
            // Current CPU Program Counter after instruction above was executed

            // Verify that the Program Counter was the first to be pushed to stack by BRK.
            // The PC address that was pushed on stack was PC location after BRK instruction was read.
            // BRK is strange. The complete instruction is only one byte but the processor increases the PC by 2.
            var originalPC = (ushort) (test.PC + 2);   
            var originalSP = test.SP;
            Assert.Equal(originalPC.Highbyte(), mem[(ushort)(CPU.StackBaseAddress + originalSP - 0)]);   // Little endian.
            Assert.Equal(originalPC.Lowbyte(),  mem[(ushort)(CPU.StackBaseAddress + originalSP - 1)]);   // Little endian.
        }

        [Fact]
        public void BRK_Pushes_A_Copy_Of_PS_With_Break_And_Unknown_Flag_Set()
        {
            var test = new TestSpec()
            {
                PC             = 0x2000,
                PS             = 0b10001011,    // Bit 4 (Break) and Bit 5 (Unused) clear when we start
                SP             = 0xff,
                OpCode         = OpCodeId.BRK,
                ExpectedSP     = 0xff - 3,      // 2 bytes for PC + 1 byte for PS
                ExpectedPS     = 0b10001011,    // The processor status should remain unchanged. See below for verification the PS pushed to stack got extra bits set.
                ExpectedI      = true,          // Interrupt flag shall always be set on PS after instruction.
            };
            var mem = test.TestContext.Mem;

            // Execute and verify instruction according to TestSpec above
            test.Execute_And_Verify(AddrMode.Implied);

            // Do additional testing not covered by TestSpec class.

            // Remember that stack works downwards (0xff-0x00), points to the next free location, and is located at address 0x0100 + SP
            var originalSP = test.SP;

            // Verify that the processor status was the second value pushed to stack.
            // Assume BRK take a copy of the process or status and set Break and Unused flag before storing the copy on stack.
            byte expectedProcessorStatusOnStack = test.PS.Value;
            expectedProcessorStatusOnStack.SetBit(StatusFlagBits.Break);
            expectedProcessorStatusOnStack.SetBit(StatusFlagBits.Unused);
            Assert.Equal(expectedProcessorStatusOnStack,  mem[(ushort)(CPU.StackBaseAddress + originalSP - 2)]);
        }

        [Fact]
        public void BRK_Changes_PC_To_Address_In_InterruptVector()
        {
            ushort brkIrqJumpAddress = 0xf000;

            var test = new TestSpec()
            {
                PC             = 0x2000,
                OpCode         = OpCodeId.BRK,
                ExpectedPC     = brkIrqJumpAddress
            };
            var mem = test.TestContext.Mem;

            // Set the memory address that the BRK instruction will look for an address to jump to (Break/IQR handler vector address)
            mem.WriteWord(CPU.BrkIRQHandlerVector, brkIrqJumpAddress);

            // Execute and verify instruction according to TestSpec above
            test.Execute_And_Verify(AddrMode.Implied);
        }
    }
}
