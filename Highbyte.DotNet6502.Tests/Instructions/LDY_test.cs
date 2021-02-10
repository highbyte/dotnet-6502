using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class LDY_test
    {
        [Fact]
        public void LDY_I_Takes_2_Cycles()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.LDY_I,
                FinalValue     = 0,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDY_I_Does_Correct_Logic_Operation()
        {
            var test = new TestSpec
            {
                Y              = 0x00,
                OpCode         = OpCodeId.LDY_I,
                FinalValue     = 0x01,
                ExpectedY      = 0x01,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDY_I_Does_Correct_Logic_Operation2()
        {
            var test = new TestSpec
            {
                Y              = 0x03,
                OpCode         = OpCodeId.LDY_I,
                FinalValue     = 0x10,
                ExpectedY      = 0x10,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDY_I_Does_Correct_Logic_Operation3()
        {
            var test = new TestSpec
            {
                Y              = 0xff,
                OpCode         = OpCodeId.LDY_I,
                FinalValue     = 0xab,
                ExpectedY      = 0xab,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDY_I_Clears_Z_If_Result_Is_Not_0()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.LDY_I,
                FinalValue     = 0x01,
                ExpectedZ      = false,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDY_I_Sets_Z_If_Result_Is_0()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.LDY_I,
                FinalValue     = 0x00,
                ExpectedZ      = true,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDY_I_Sets_N_If_Result_Is_Negative()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.LDY_I,
                FinalValue     = 0xff,
                ExpectedN      = true,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDY_I_Clears_N_If_Result_Is_Positive()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.LDY_I,
                FinalValue     = 0x10,
                ExpectedN      = false,
            };
            test.Execute_And_Verify(AddrMode.I);
        }        

        // ----------------------------------------------------------------------------------------
        // Other addressing modes than _I
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Only test addressing mode works, the binary operation where tested
        /// in the LDY_I instruction above, and are used by all addressing modes.
        /// </summary>
        [Fact]
        public void LDY_ZP_Works()
        {
            var test = new TestSpec()
            {
                Y              = 0x00,
                OpCode         = OpCodeId.LDY_ZP,
                FinalValue     = 0x12,
                ExpectedY      = 0x12,
                ExpectedCycles = 3
            };
            test.Execute_And_Verify(AddrMode.ZP);
        }

        [Fact]
        public void LDY_ZP_X_Works()
        {
            var test = new TestSpec()
            {
                Y              = 0x00,
                OpCode         = OpCodeId.LDY_ZP_X,
                FinalValue     = 0x12,
                ExpectedY      = 0x12,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ZP_X);
        }


        /// <summary>
        /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
        /// </summary>
        [Fact]
        public void LDY_ZP_X_Where_ZeroPage_Address_Plus_X_Wraps_Over_Byte_Size_Works()
        {
            var test = new TestSpec()
            {
                Y              = 0x00,
                OpCode         = OpCodeId.LDY_ZP_X,
                FinalValue     = 0x12,
                ExpectedY      = 0x12,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ZP_X, ZP_X_Should_Wrap_Over_Byte: true);
        }

        [Fact]
        public void LDY_ABS_Works()
        {
            var test = new TestSpec()
            {
                Y              = 0x00,
                OpCode         = OpCodeId.LDY_ABS,
                FinalValue     = 0x12,
                ExpectedY      = 0x12,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ABS);
        }

        [Fact]
        public void LDY_ABS_X_Works()
        {
            var test = new TestSpec()
            {
                Y              = 0x00,
                OpCode         = OpCodeId.LDY_ABS_X,
                FinalValue     = 0x12,
                ExpectedY      = 0x12,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ABS_X);
        }

        [Fact]
        public void LDY_ABS_X_Where_Address_Plus_X_Crosses_Page_Boundary_Works()
        {
            var test = new TestSpec()
            {
                Y              = 0x00,
                OpCode         = OpCodeId.LDY_ABS_X,
                FinalValue     = 0x12,
                ExpectedY      = 0x12,
                ExpectedCycles = 5
            };
            test.Execute_And_Verify(AddrMode.ABS_X, FullAddress_Should_Cross_Page_Boundary: true);
        }
    }
}
