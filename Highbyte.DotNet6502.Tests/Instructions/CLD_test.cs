using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class CLD_test
    {
        [Fact]
        public void CLD_Takes_2_Cycles()
        {
            var test = new TestSpec()
            {
                Instruction    = Ins.CLD,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void CLD_Clears_Decimal_Flag()
        {
            var test = new TestSpec()
            {
                D              = true,
                Instruction    = Ins.CLD,
                ExpectedD      = false,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }
    }
}
