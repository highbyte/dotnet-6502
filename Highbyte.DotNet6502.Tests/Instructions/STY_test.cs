using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class STY_test
    {
        [Fact]
        public void STY_ZP_Takes_3_Cycles()
        {
            var test = new TestSpec()
            {
                InsEffect      = InstrEffect.Mem,
                Instruction    = OpCodeId.STY_ZP,
                ExpectedCycles = 3,
            };
            test.Execute_And_Verify(AddrMode.ZP);
        }

        [Fact]
        public void STY_ZP_Stores_y_Register_To_Memory()
        {
            var test = new TestSpec
            {
                Y              = 0x42,
                InsEffect      = InstrEffect.Mem,    // Defaults to Read
                Instruction    = OpCodeId.STY_ZP,
                FinalValue     = 0x01, // Initial value at memory the instruction will write to. If not specified, a default value will be written there before instruction executes.
                //ZeroPageAddress = 0x30, // Optional ZeroPage address. If not specified, a default address is used.
                ExpectedMemVal = 0x42, // Should be the value we had in A register before instruction executes.
                ExpectedCycles = 3,
            };
            test.Execute_And_Verify(AddrMode.ZP);
        }

        // ----------------------------------------------------------------------------------------
        // Other addressing modes than _ZP
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Only test addressing mode works, the binary arithmetic (carry, zero, overflow flags) where tested
        /// in the STY_ZP instruction above, and are used by all addressing modes.
        /// </summary>
        [Fact]
        public void STY_ZP_X_Works()
        {
            var test = new TestSpec
            {
                Y              = 0x42,
                InsEffect      = InstrEffect.Mem,
                Instruction    = OpCodeId.STY_ZP_X,
                FinalValue     = 0x01,
                ExpectedMemVal = 0x42,
                ExpectedCycles = 4,
            };
            test.Execute_And_Verify(AddrMode.ZP_X);
        }

        /// <summary>
        /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
        /// If we repeat the last example but with $FF in the X register then the accumulator will be stored to memory $007F (e.g. $80 + $FF => $7F) and not $017F.
        /// </summary>
        [Fact]
        public void STY_ZP_Z_Where_ZeroPage_Address_Plus_Z_Wraps_Over_Byte_Size_Works()
        {
            var test = new TestSpec
            {
                Y              = 0x42,
                InsEffect      = InstrEffect.Mem,
                Instruction    = OpCodeId.STY_ZP_X,
                FinalValue     = 0x01,
                ExpectedMemVal = 0x42,
                ExpectedCycles = 4,
            };
            test.Execute_And_Verify(AddrMode.ZP_X, ZP_X_Should_Wrap_Over_Byte: true);
        }

        [Fact]
        public void STY_ABS_Works()
        {
            var test = new TestSpec
            {
                Y              = 0x42,
                InsEffect      = InstrEffect.Mem,
                Instruction    = OpCodeId.STY_ABS,
                FinalValue     = 0x01,
                ExpectedMemVal = 0x42,
                ExpectedCycles = 4,
            };
            test.Execute_And_Verify(AddrMode.ABS);
        }
    }
}
