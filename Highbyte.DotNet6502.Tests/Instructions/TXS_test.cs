using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class TXS_test
    {
        [Fact]
        public void TXS_Takes_2_Cycles()
        {
            var test = new TestSpec()
            {
                Instruction    = OpCodeId.TXS,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void TXS_Transfers_X_To_SP()
        {
            var test = new TestSpec()
            {
                X              = 0x34,
                SP             = 0x80,
                Instruction    = OpCodeId.TXS,
                ExpectedSP     = 0x34,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

                [Fact]
        public void TXS_Does_Not_Affect_Status_Flags()
        {
            var test = new TestSpec()
            {
                PS             = 0xff,
                X              = 0x34,
                SP             = 0x80,
                Instruction    = OpCodeId.TXS,
                ExpectedPS     = 0xff
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }
    }
}
