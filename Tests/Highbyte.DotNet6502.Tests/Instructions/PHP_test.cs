namespace Highbyte.DotNet6502.Tests.Instructions;

public class PHP_test
{
    [Fact]
    public void PHP_Takes_3_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.PHP,
            ExpectedCycles = 3,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void PHP_Pushes_Copy_Of_PS_To_Stack_With_Break_And_Unused_Flag_Set()
    {
        var test = new TestSpec()
        {
            SP             = 0xff,
            C              = false, // Status bit 0
            Z              = true,
            I              = false,
            D              = true,
            B              = false, // Status bit 4 (Break) clear when we start
            U              = false, // Status bit 5 (Unused) clear when we start
            V              = false,
            N              = true,  // Status bit 7
            OpCode         = OpCodeId.PHP,
        };
        test.Execute_And_Verify(AddrMode.Implied);

        // Verify that stack (the previous position) contains value of Status register.
        // Remember that stack works downwards (0xff-0x00), points to the next free location, and is located at address 0x0100 + SP
        ushort stackPointerFullAddress = CPU.StackBaseAddress + 0xfe + 1;
        byte expectedPSOnStack = test.PS.Value;
        expectedPSOnStack.SetBit(StatusFlagBits.Break); 
        expectedPSOnStack.SetBit(StatusFlagBits.Unused); 
        Assert.Equal(expectedPSOnStack, test.TestContext.Mem[stackPointerFullAddress]);
    }

    [Fact]
    public void PHP_Has_No_Side_Effects_On_Current_PS()
    {
        var test = new TestSpec()
        {
            SP             = 0xff,
            C              = false, // Status bit 0
            Z              = true,
            I              = false,
            D              = true,
            B              = false,
            U              = true,
            V              = false,
            N              = true,  // Status bit 7
            OpCode         = OpCodeId.PHP,
            ExpectedC      = false, // Status bit 0
            ExpectedZ      = true,
            ExpectedI      = false,
            ExpectedD      = true,
            ExpectedB      = false,
            ExpectedU      = true,
            ExpectedV      = false,
            ExpectedN      = true,  // Status bit 7
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }
}
