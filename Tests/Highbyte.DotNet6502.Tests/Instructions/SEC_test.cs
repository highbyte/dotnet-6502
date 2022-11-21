namespace Highbyte.DotNet6502.Tests.Instructions;

public class SEC_test
{
    [Fact]
    public void SEC_Takes_2_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.SEC,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void SEC_Sets_Carry_Flag()
    {
        var test = new TestSpec()
        {
            C              = false,
            OpCode         = OpCodeId.SEC,
            ExpectedC      = true,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }
}
