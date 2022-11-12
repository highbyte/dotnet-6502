using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class TAY_test
    {
        [Fact]
        public void TAY_Takes_2_Cycles()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.TAY,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void TAY_Transfers_A_To_Y()
        {
            var test = new TestSpec()
            {
                A              = 0x34,
                Y              = 0x22,
                OpCode         = OpCodeId.TAY,
                ExpectedY      = 0x34,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void TAY_ZP_Sets_Zero_Flag_If_Result_Is_Zero()
        {
            var test = new TestSpec()
            {
                A              = 0x00,
                Y              = 0x01,
                OpCode         = OpCodeId.TAY,
                ExpectedZ      = true,
                ExpectedY      = 0x00,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void TAY_ZP_Clears_Zero_Flag_If_Result_Is_Not_Zero()
        {
            var test = new TestSpec()
            {
                A              = 0x34,
                Y              = 0x01,
                OpCode         = OpCodeId.TAY,
                ExpectedZ      = false,
                ExpectedY      = 0x34,
            };
            test.Execute_And_Verify(AddrMode.Implied);        
        }

        [Fact]
        public void TAY_ZP_Sets_Negative_Flag_If_Result_Is_Negative()
        {
            var test = new TestSpec()
            {
                A              = 0xfe,
                Y              = 0x00,
                OpCode         = OpCodeId.TAY,
                ExpectedN      = true,
                ExpectedY      = 0xfe,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void TAY_ZP_Clears_Negative_Flag_If_Result_Is_Positive()
        {
            var test = new TestSpec()
            {
                A              = 0x34,
                Y              = 0x01,
                OpCode         = OpCodeId.TAY,
                ExpectedN      = false,
                ExpectedY      = 0x34,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }
    }
}
