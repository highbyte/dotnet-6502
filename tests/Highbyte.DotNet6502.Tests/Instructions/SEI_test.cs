namespace Highbyte.DotNet6502.Tests.Instructions;

public class SEI_test
{
    [Fact]
    public void SEI_Takes_2_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.SEI,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void SEI_Sets_InterruptDisable_Flag()
    {
        var test = new TestSpec()
        {
            I              = false,
            OpCode         = OpCodeId.SEI,
            ExpectedI      = true,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }
}
