using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class CLI_test
    {
        [Fact]
        public void CLI_Takes_2_Cycles()
        {
            var test = new TestSpec()
            {
                Instruction    = OpCodeId.CLI,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void CLI_Clears_InterruptDisable_Flag()
        {
            var test = new TestSpec()
            {
                I              = true,
                Instruction    = OpCodeId.CLI,
                ExpectedI      = false,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }
    }
}
