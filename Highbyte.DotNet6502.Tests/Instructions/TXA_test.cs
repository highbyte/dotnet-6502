using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class TXA_test
    {
        [Fact]
        public void TXA_Takes_2_Cycles()
        {
            var test = new TestSpec()
            {
                Instruction    = OpCodeId.TXA,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void TXA_Transfers_X_To_A()
        {
            var test = new TestSpec()
            {
                X              = 0x22,
                A              = 0x34,
                Instruction    = OpCodeId.TXA,
                ExpectedA      = 0x22,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void TXA_Sets_Zero_Flag_If_Result_Is_Zero()
        {
            var test = new TestSpec()
            {
                X              = 0x00,
                A              = 0x34,
                Instruction    = OpCodeId.TXA,
                ExpectedZ      = true,
                ExpectedA      = 0x00,
            };
            test.Execute_And_Verify(AddrMode.Implied);        }

        [Fact]
        public void TXA_Clears_Zero_Flag_If_Result_Is_Not_Zero()
        {
            var test = new TestSpec()
            {
                X              = 0x01,
                A              = 0x34, 
                Instruction    = OpCodeId.TXA,
                ExpectedZ      = false,
                ExpectedA      = 0x01,
            };
            test.Execute_And_Verify(AddrMode.Implied);        
        }

        [Fact]
        public void TXA_Sets_Negative_Flag_If_Result_Is_Negative()
        {
            var test = new TestSpec()
            {
                X              = 0xfe,
                A              = 0x34,
                Instruction    = OpCodeId.TXA,
                ExpectedN      = true,
                ExpectedA      = 0xfe,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void TXA_Clears_Negative_Flag_If_Result_Is_Positive()
        {
            var test = new TestSpec()
            {
                X              = 0x01,
                A              = 0x34,
                Instruction    = OpCodeId.TXA,
                ExpectedN      = false,
                ExpectedA      = 0x01,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }
    }
}
