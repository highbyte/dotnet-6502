using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class LDX_test
    {
        [Fact]
        public void LDX_I_Takes_2_Cycles()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.LDX_I,
                FinalValue     = 0,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDX_I_Does_Correct_Logic_Operation()
        {
            var test = new TestSpec
            {
                X              = 0x00,
                OpCode         = OpCodeId.LDX_I,
                FinalValue     = 0x01,
                ExpectedX      = 0x01,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDX_I_Does_Correct_Logic_Operation2()
        {
            var test = new TestSpec
            {
                X              = 0x03,
                OpCode         = OpCodeId.LDX_I,
                FinalValue     = 0x10,
                ExpectedX      = 0x10,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDX_I_Does_Correct_Logic_Operation3()
        {
            var test = new TestSpec
            {
                X              = 0xff,
                OpCode         = OpCodeId.LDX_I,
                FinalValue     = 0xab,
                ExpectedX      = 0xab,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDX_I_Clears_Z_If_Result_Is_Not_0()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.LDX_I,
                FinalValue     = 0x01,
                ExpectedZ      = false,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDX_I_Sets_Z_If_Result_Is_0()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.LDX_I,
                FinalValue     = 0x00,
                ExpectedZ      = true,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDX_I_Sets_N_If_Result_Is_Negative()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.LDX_I,
                FinalValue     = 0xff,
                ExpectedN      = true,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void LDX_I_Clears_N_If_Result_Is_Positive()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.LDX_I,
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
        /// in the LDX_I instruction above, and are used by all addressing modes.
        /// </summary>
        [Fact]
        public void LDX_ZP_Works()
        {
            var test = new TestSpec()
            {
                X              = 0x00,
                OpCode         = OpCodeId.LDX_ZP,
                FinalValue     = 0x12,
                ExpectedX      = 0x12,
                ExpectedCycles = 3
            };
            test.Execute_And_Verify(AddrMode.ZP);
        }

        [Fact]
        public void LDX_ZP_Y_Works()
        {
            var test = new TestSpec()
            {
                X              = 0x00,
                OpCode         = OpCodeId.LDX_ZP_Y,
                FinalValue     = 0x12,
                ExpectedX      = 0x12,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ZP_Y);
        }


        /// <summary>
        /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
        /// </summary>
        [Fact]
        public void LDX_ZP_Y_Where_ZeroPage_Address_Plus_Y_Wraps_Over_Byte_Size_Works()
        {
            var test = new TestSpec()
            {
                X              = 0x00,
                OpCode         = OpCodeId.LDX_ZP_Y,
                FinalValue     = 0x12,
                ExpectedX      = 0x12,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ZP_Y, ZP_Y_Should_Wrap_Over_Byte: true);
        }

        [Fact]
        public void LDX_ABS_Works()
        {
            var test = new TestSpec()
            {
                X              = 0x00,
                OpCode         = OpCodeId.LDX_ABS,
                FinalValue     = 0x12,
                ExpectedX      = 0x12,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ABS);
        }

        [Fact]
        public void LDX_ABS_Y_Works()
        {
            var test = new TestSpec()
            {
                X              = 0x00,
                OpCode         = OpCodeId.LDX_ABS_Y,
                FinalValue     = 0x12,
                ExpectedX      = 0x12,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ABS_Y);
        }

        [Fact]
        public void LDX_ABS_Y_Where_Address_Plus_Y_Crosses_Page_Boundary_Works()
        {
            var test = new TestSpec()
            {
                X              = 0x00,
                OpCode         = OpCodeId.LDX_ABS_Y,
                FinalValue     = 0x12,
                ExpectedX      = 0x12,
                ExpectedCycles = 5
            };
            test.Execute_And_Verify(AddrMode.ABS_Y, FullAddress_Should_Cross_Page_Boundary: true);
        }
    }
}
