namespace Highbyte.DotNet6502.Tests.Instructions;

public class RTI_test
{
    [Fact]
    public void RTI_Takes_6_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.RTI,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void RTI_Resumes_Execution_Where_BRK_Was_Issued_By_Poping_PC_From_Stack()
    {

        ushort returnAddressOnStack = 0x2001;

        var test = new TestSpec()
        {
            SP             = 0xfc,
            PS             = 0x33,
            OpCode         = OpCodeId.RTI,
            ExpectedPC     = returnAddressOnStack,
            ExpectedSP     = 0xfc + 3       // 1 byte for processor status, 2 bytes for return address
        };

        var mem = test.TestContext.Mem;

        // The return address on stack next. Little endian.
        mem[(ushort)(CPU.StackBaseAddress + test.SP + 2)] = returnAddressOnStack.Lowbyte();
        mem[(ushort)(CPU.StackBaseAddress + test.SP + 3)] = returnAddressOnStack.Highbyte(); 

        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void RTI_Pops_PS_From_Stack_And_Clears_Break_And_Unknown_Flags()
    {
        byte psOnStack = 0x01;
        psOnStack.SetBit(StatusFlagBits.Break); // The processor status on the stack will always (?) have Break flag set by the BRK instruction.
        psOnStack.SetBit(StatusFlagBits.Unused); // The processor status on the stack will always (?) have Unused flag set by the BRK instruction.

        byte expectedPSAfterInstruction = psOnStack;
        expectedPSAfterInstruction.ClearBit(StatusFlagBits.Break); // The when poping the PS from stack, the instruction also clears Break flag
        expectedPSAfterInstruction.ClearBit(StatusFlagBits.Unused); // The when poping the PS from stack, the instruction also clears Break flag

        var test = new TestSpec()
        {
            SP             = 0xfc,
            PS             = 0x33,
            OpCode         = OpCodeId.RTI,
            ExpectedPS     = expectedPSAfterInstruction,
            ExpectedSP     = 0xfc + 3       // 1 byte for processor status, 2 bytes for return address
        };

        var mem = test.TestContext.Mem;

        // The processor status pushed to stack by BRK instruction (it was written last, but will be read in reverse order when it's popped)
        // The current SP always points to the next free location. So the last location that was used (by the BRK instruction) is one up (remember stack goes downwards)
        mem[(ushort)(CPU.StackBaseAddress + test.SP + 1)] = psOnStack;

        test.Execute_And_Verify(AddrMode.Implied);
    }
}
