using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class SED_test
    {
        [Fact]
        public void SED_Takes_2_Cycles()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.SED,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void SED_Sets_Decimal_Flag()
        {
            var test = new TestSpec()
            {
                D              = false,
                OpCode         = OpCodeId.SED,
                ExpectedD      = true,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }
    }
}
