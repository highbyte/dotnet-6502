using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class CPY_test
    {
        [Fact]
        public void CPY_I_Takes_2_Cycles()
        {
            var test = new TestSpec()
            {
                Instruction    = Ins.CPY_I,
                FinalValue     = 0,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPY_I_Sets_Carry_If_A_Is_Greater_Than_Value()
        {
            var test = new TestSpec
            {
                Y              = 0x02,
                Instruction    = Ins.CPY_I,
                FinalValue     = 0x01,  // The value A register is compared against
                ExpectedC      = true
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPY_I_Sets_Carry_If_A_Is_Equal_To_Value()
        {
            var test = new TestSpec
            {
                Y              = 0x01,
                Instruction    = Ins.CPY_I,
                FinalValue     = 0x01,  // The value A register is compared against
                ExpectedC      = true
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPY_I_Clears_Carry_If_A_Is_Less_Than_Value()
        {
            var test = new TestSpec
            {
                Y              = 0x01,
                Instruction    = Ins.CPY_I,
                FinalValue     = 0x02,  // The value A register is compared against
                ExpectedC      = false
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPY_I_Ignores_Signed_Number_In_Comparision()
        {
            var test = new TestSpec
            {
                Y              = 0xff,  // 255. Always treated as signed, not as -1
                Instruction    = Ins.CPY_I,
                FinalValue     = 0x01,  // The value A register is compared against
                ExpectedC      = true   // Because 0xff is bigger than 0x01 (sign is always ignored!)
            };
            test.Execute_And_Verify(AddrMode.I);
        }        

        [Fact]
        public void CPY_I_Sets_Zero_If_A_Is_Equal_To_Value()
        {
            var test = new TestSpec
            {
                Y              = 0x01,
                Instruction    = Ins.CPY_I,
                FinalValue     = 0x01,  // The value A register is compared against
                ExpectedZ      = true
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPY_I_Clears_Zero_If_A_Is_Not_Equal_To_Value()
        {
            var test = new TestSpec
            {
                Y              = 0x01,
                Instruction    = Ins.CPY_I,
                FinalValue     = 0x02,  // The value A register is compared against
                ExpectedZ      = false
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPY_I_Sets_N_If_A_Minus_Value_Is_Negative()
        {
            var test = new TestSpec()
            {
                Y              = 0x01,
                Instruction    = Ins.CPY_I,
                FinalValue     = 0x02,
                ExpectedN      = true,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPY_I_Clears_N_If_A_Minus_Value_Is_Positive()
        {
            var test = new TestSpec()
            {
                Y              = 0x02,
                Instruction    = Ins.CPY_I,
                FinalValue     = 0x01,
                ExpectedN      = false,
            };
            test.Execute_And_Verify(AddrMode.I);
        }        

        // ----------------------------------------------------------------------------------------
        // Other addressing modes than _I
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Only test addressing mode works, the binary operation where tested
        /// in the CPY_I instruction above, and are used by all addressing modes.
        /// </summary>
        [Fact]
        public void CPY_ZP_Works()
        {
            var test = new TestSpec()
            {
                Y              = 0x33,
                Instruction    = Ins.CPY_ZP,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,

            };
            test.Execute_And_Verify(AddrMode.ZP);
        }

        [Fact]
        public void CPY_ABS_Works()
        {
            var test = new TestSpec()
            {
                Y              = 0x33,
                Instruction    = Ins.CPY_ABS,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ABS);
        }

    }
}
