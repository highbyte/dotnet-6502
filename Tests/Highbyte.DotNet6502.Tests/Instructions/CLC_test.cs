using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions;

public class CLC_test
{
    [Fact]
    public void CLC_Takes_2_Of_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.CLC,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void CLC_Clears_Carry_Flag()
    {
        var test = new TestSpec()
        {
            C              = true,
            OpCode         = OpCodeId.CLC,
            ExpectedC      = false,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }
}
