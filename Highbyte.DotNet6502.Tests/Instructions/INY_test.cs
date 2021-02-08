using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class INY_test
    {
        [Fact]
        public void INY_Takes_2_Cycles()
        {
            var test = new TestSpec()
            {
                Instruction    = Ins.INY,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void INY_Increases_Register()
        {
            var test = new TestSpec()
            {
                Y              = 0x01,
                Instruction    = Ins.INY,
                ExpectedY      = 0x02,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void INY_Sets_Zero_Flag_If_Result_Is_Zero()
        {
            var test = new TestSpec()
            {
                Y              = 0xff,
                Instruction    = Ins.INY,
                ExpectedY      = 0x00,
                ExpectedZ      = true,
            };
            test.Execute_And_Verify(AddrMode.Implied);        }

        [Fact]
        public void INY_Clears_Zero_Flag_If_Result_Is_Not_Zero()
        {
            var test = new TestSpec()
            {
                Y              = 0x00,
                Instruction    = Ins.INY,
                ExpectedY      = 0x01,
                ExpectedZ      = false,
            };
            test.Execute_And_Verify(AddrMode.Implied);        
        }

        [Fact]
        public void INY_Sets_Negative_Flag_If_Result_Is_Negative()
        {
            var test = new TestSpec()
            {
                Y              = 0xfe,
                Instruction    = Ins.INY,
                ExpectedY      = 0xff,
                ExpectedN      = true,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void INY_Clears_Negative_Flag_If_Result_Is_Positive()
        {
            var test = new TestSpec()
            {
                Y              = 0x01,
                Instruction    = Ins.INY,
                ExpectedY      = 0x02,
                ExpectedN      = false,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }
    }
}
