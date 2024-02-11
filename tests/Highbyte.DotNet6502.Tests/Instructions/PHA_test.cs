namespace Highbyte.DotNet6502.Tests.Instructions;

public class PHA_test
{
    [Fact]
    public void PHA_Takes_3_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.PHA,
            ExpectedCycles = 3,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void PHA_Executes_With_No_Side_Effects_Other_Than_Pushing_A_To_Stack()
    {
        var test = new TestSpec()
        {
            SP             = 0xff,
            C              = false,
            Z              = false,
            I              = false,
            D              = false,
            B              = false,
            U              = false,
            V              = false,
            N              = false,
            OpCode         = OpCodeId.PHA,
            A              = 0x12,
            ExpectedSP     = 0xfe,
            ExpectedC      = false,
            ExpectedZ      = false,
            ExpectedI      = false,
            ExpectedD      = false,
            ExpectedB      = false,
            ExpectedU      = false,
            ExpectedV      = false,
            ExpectedN      = false,
        };
        test.Execute_And_Verify(AddrMode.Implied);

        // Verify that stack (the previous position) contains value of A register.
        // Remember that stack works downwards (0xff-0x00), points to the next free location, and is located at address 0x0100 + SP
        ushort stackPointerFullAddress = CPU.StackBaseAddress + 0xfe + 1;
        Assert.Equal(test.A, test.TestContext.Mem[stackPointerFullAddress]);
    }
}
