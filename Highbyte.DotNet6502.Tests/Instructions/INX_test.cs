using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class INX_test
    {
        [Fact]
        public void INX_Takes_2_Cycles()
        {
            var test = new TestSpec()
            {
                Instruction    = OpCodeId.INX,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void INX_Increases_Register()
        {
            var test = new TestSpec()
            {
                X              = 0x01,
                Instruction    = OpCodeId.INX,
                ExpectedX      = 0x02,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void INX_Sets_Zero_Flag_If_Result_Is_Zero()
        {
            var test = new TestSpec()
            {
                X              = 0xff,
                Instruction    = OpCodeId.INX,
                ExpectedX      = 0x00,
                ExpectedZ      = true,
            };
            test.Execute_And_Verify(AddrMode.Implied);        }

        [Fact]
        public void INX_Clears_Zero_Flag_If_Result_Is_Not_Zero()
        {
            var test = new TestSpec()
            {
                X              = 0x00,
                Instruction    = OpCodeId.INX,
                ExpectedX      = 0x01,
                ExpectedZ      = false,
            };
            test.Execute_And_Verify(AddrMode.Implied);        
        }

        [Fact]
        public void INX_Sets_Negative_Flag_If_Result_Is_Negative()
        {
            var test = new TestSpec()
            {
                X              = 0xfe,
                Instruction    = OpCodeId.INX,
                ExpectedX      = 0xff,
                ExpectedN      = true,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void INX_Clears_Negative_Flag_If_Result_Is_Positive()
        {
            var test = new TestSpec()
            {
                X              = 0x01,
                Instruction    = OpCodeId.INX,
                ExpectedX      = 0x02,
                ExpectedN      = false,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }
    }
}
