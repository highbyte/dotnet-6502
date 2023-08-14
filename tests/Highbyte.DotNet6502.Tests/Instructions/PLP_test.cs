namespace Highbyte.DotNet6502.Tests.Instructions;

public class PLP_test
{
    [Fact]
    public void PLP_Takes_4_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.PLP,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void PLP_Pops_PS_From_Stack()
    {

        var test = new TestSpec()
        {
            SP             = 0xfe,
            PS             = 0x00,
            OpCode         = OpCodeId.PLP,
            ExpectedPS     = 0x12,
            ExpectedSP     = 0xff,
        };

        // Prepare a value on the stack (one address higher than the current SP we use in test)
        // Remember that stack works downwards (0xff-0x00), points to the next free location, and is located at address 0x0100 + SP
        ushort stackPointerFullAddress = CPU.StackBaseAddress + 0xff;
        test.TestContext.Mem[stackPointerFullAddress] = 0x12;

        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void PLP_Pops_PS_From_Stack_With_All_Status_Bits_Set()
    {

        var test = new TestSpec()
        {
            SP             = 0xfe,
            PS             = 0x00,
            OpCode         = OpCodeId.PLP,
            ExpectedPS     = 0xff,
            ExpectedSP     = 0xff,
        };

        // Prepare a value on the stack (one address higher than the current SP we use in test)
        // Remember that stack works downwards (0xff-0x00), points to the next free location, and is located at address 0x0100 + SP
        ushort stackPointerFullAddress = CPU.StackBaseAddress + 0xff;
        test.TestContext.Mem[stackPointerFullAddress] = 0xff;

        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void PLP_Pops_Correct_Byte_From_Stack_When_SP_Wraps_Around_Byte_Limit()
    {

        byte expectedValueFromStack = 0x42;

        // We'll try to pop a byte from stack when it's at the top (nothing should be on stack then.).
        // This should lead to we pop the byte at 0x0100, and not 0x0200.
        var test = new TestSpec()
        {
            SP             = 0xff,
            PS             = 0x00,
            OpCode         = OpCodeId.PLP,
            ExpectedSP     = 0x00,
            ExpectedPS     = expectedValueFromStack,
        };

        // Prepare a value on the stack (one address higher than the current SP we use in test)
        // Remember that stack works downwards (0xff-0x00), points to the next free location, and is located at address 0x0100 + SP
        byte SPToReadFrom = (byte)(test.SP + 1);
        ushort stackPointerFullAddressShouldPopFrom = (ushort) (CPU.StackBaseAddress  + SPToReadFrom);
        test.TestContext.Mem[stackPointerFullAddressShouldPopFrom] = expectedValueFromStack;
        // The incorrect memory position with another value
        test.TestContext.Mem[(ushort)(CPU.StackBaseAddress + test.SP + 1)] = (byte)(expectedValueFromStack-1); 

        test.Execute_And_Verify(AddrMode.Implied);
    }
}
