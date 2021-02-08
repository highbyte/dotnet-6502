using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class CPX_test
    {
        [Fact]
        public void CPX_I_Takes_2_Cycles()
        {
            var test = new TestSpec()
            {
                Instruction    = Ins.CPX_I,
                FinalValue     = 0,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPX_I_Sets_Carry_If_A_Is_Greater_Than_Value()
        {
            var test = new TestSpec
            {
                X              = 0x02,
                Instruction    = Ins.CPX_I,
                FinalValue     = 0x01,  // The value A register is compared against
                ExpectedC      = true
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPX_I_Sets_Carry_If_A_Is_Equal_To_Value()
        {
            var test = new TestSpec
            {
                X              = 0x01,
                Instruction    = Ins.CPX_I,
                FinalValue     = 0x01,  // The value A register is compared against
                ExpectedC      = true
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPX_I_Clears_Carry_If_A_Is_Less_Than_Value()
        {
            var test = new TestSpec
            {
                X              = 0x01,
                Instruction    = Ins.CPX_I,
                FinalValue     = 0x02,  // The value A register is compared against
                ExpectedC      = false
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPX_I_Ignores_Signed_Number_In_Comparision()
        {
            var test = new TestSpec
            {
                X              = 0xff,  // 255. Always treated as signed, not as -1
                Instruction    = Ins.CPX_I,
                FinalValue     = 0x01,  // The value A register is compared against
                ExpectedC      = true   // Because 0xff is bigger than 0x01 (sign is always ignored!)
            };
            test.Execute_And_Verify(AddrMode.I);
        }        

        [Fact]
        public void CPX_I_Sets_Zero_If_A_Is_Equal_To_Value()
        {
            var test = new TestSpec
            {
                X              = 0x01,
                Instruction    = Ins.CPX_I,
                FinalValue     = 0x01,  // The value A register is compared against
                ExpectedZ      = true
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPX_I_Clears_Zero_If_A_Is_Not_Equal_To_Value()
        {
            var test = new TestSpec
            {
                X              = 0x01,
                Instruction    = Ins.CPX_I,
                FinalValue     = 0x02,  // The value A register is compared against
                ExpectedZ      = false
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPX_I_Sets_N_If_A_Minus_Value_Is_Negative()
        {
            var test = new TestSpec()
            {
                X              = 0x01,
                Instruction    = Ins.CPX_I,
                FinalValue     = 0x02,
                ExpectedN      = true,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CPX_I_Clears_N_If_A_Minus_Value_Is_Positive()
        {
            var test = new TestSpec()
            {
                X              = 0x02,
                Instruction    = Ins.CPX_I,
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
        /// in the CPX_I instruction above, and are used by all addressing modes.
        /// </summary>
        [Fact]
        public void CPX_ZP_Works()
        {
            var test = new TestSpec()
            {
                X              = 0x33,
                Instruction    = Ins.CPX_ZP,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 3,
            };
            test.Execute_And_Verify(AddrMode.ZP);
        }

        [Fact]
        public void CPX_ABS_Works()
        {
            var test = new TestSpec()
            {
                X              = 0x33,
                Instruction    = Ins.CPX_ABS,
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
