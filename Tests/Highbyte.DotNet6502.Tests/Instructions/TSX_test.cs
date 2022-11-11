using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class TSX_test
    {
        [Fact]
        public void TSX_Takes_2s_Cycles()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.TSX,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void TSX_Transfers_SP_To_X()
        {
            var test = new TestSpec()
            {
                SP             = 0x80,
                X              = 0x34,
                OpCode         = OpCodeId.TSX,
                ExpectedX      = 0x80,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void TSX_ZP_Sets_Zero_Flag_If_Result_Is_Zero()
        {
            var test = new TestSpec()
            {
                SP             = 0x00,
                X              = 0x34,
                OpCode         = OpCodeId.TSX,
                ExpectedZ      = true,
                ExpectedX      = 0x00,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void TSX_ZP_Clears_Zero_Flag_If_Result_Is_Not_Zero()
        {
            var test = new TestSpec()
            {
                SP             = 0x34,
                X              = 0x01,
                OpCode         = OpCodeId.TSX,
                ExpectedZ      = false,
                ExpectedX      = 0x34,
            };
            test.Execute_And_Verify(AddrMode.Implied);        
        }

        [Fact]
        public void TSX_ZP_Sets_Negative_Flag_If_Result_Is_Negative()
        {
            var test = new TestSpec()
            {
                SP             = 0xfe,
                X              = 0x34,
                OpCode         = OpCodeId.TSX,
                ExpectedN      = true,
                ExpectedX      = 0xfe,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void TSX_ZP_Clears_Negative_Flag_If_Result_Is_Positive()
        {
            var test = new TestSpec()
            {
                SP             = 0x34,
                X              = 0x01,
                OpCode         = OpCodeId.TSX,
                ExpectedN      = false,
                ExpectedX      = 0x34,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }
    }
}
