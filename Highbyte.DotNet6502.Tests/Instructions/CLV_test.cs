using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class CLV_test
    {
        [Fact]
        public void CLV_Takes_Takes_2_Cycles()
        {
            var test = new TestSpec()
            {
                Instruction    = Ins.CLV,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void CLV_Clears_Overflow_Flag()
        {
            var test = new TestSpec()
            {
                V              = true,
                Instruction    = Ins.CLV,
                ExpectedV      = false,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }
    }
}
