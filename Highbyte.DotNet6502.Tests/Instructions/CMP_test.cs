using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class CMP_test
    {
        [Fact]
        public void CMP_I_Takes_2_Of_Cycles()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.CMP_I,
                FinalValue     = 0,
                ExpectedCycles = 2,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CMP_I_Sets_Carry_If_A_Is_Greater_Than_Value()
        {
            var test = new TestSpec
            {
                A              = 0x02,
                OpCode         = OpCodeId.CMP_I,
                FinalValue     = 0x01,  // The value A register is compared against
                ExpectedC      = true
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CMP_I_Sets_Carry_If_A_Is_Equal_To_Value()
        {
            var test = new TestSpec
            {
                A              = 0x01,
                OpCode         = OpCodeId.CMP_I,
                FinalValue     = 0x01,  // The value A register is compared against
                ExpectedC      = true
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CMP_I_Clears_Carry_If_A_Is_Less_Than_Value()
        {
            var test = new TestSpec
            {
                A              = 0x01,
                OpCode         = OpCodeId.CMP_I,
                FinalValue     = 0x02,  // The value A register is compared against
                ExpectedC      = false
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CMP_I_Ignores_SignedNumber_In_Comparision()
        {
            var test = new TestSpec
            {
                A              = 0xff,  // 255. Always treated as signed, not as -1
                OpCode         = OpCodeId.CMP_I,
                FinalValue     = 0x01,  // The value A register is compared against
                ExpectedC      = true   // Because 0xff is bigger than 0x01 (sign is always ignored!)
            };
            test.Execute_And_Verify(AddrMode.I);
        }        

        [Fact]
        public void CMP_I_Sets_Zero_If_A_Is_Equal_To_Value()
        {
            var test = new TestSpec
            {
                A              = 0x01,
                OpCode         = OpCodeId.CMP_I,
                FinalValue     = 0x01,  // The value A register is compared against
                ExpectedZ      = true
            };
            test.Execute_And_Verify(AddrMode.I);
        }


        [Fact]
        public void CMP_I_Clears_Zero_If_A_Is_Not_Equal_To_Value()
        {
            var test = new TestSpec
            {
                A              = 0x01,
                OpCode         = OpCodeId.CMP_I,
                FinalValue     = 0x02,  // The value A register is compared against
                ExpectedZ      = false
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CMP_I_Sets_N_If_A_Minus_Value_Is_Negative()
        {
            var test = new TestSpec()
            {
                A              = 0x01,
                OpCode         = OpCodeId.CMP_I,
                FinalValue     = 0x02,
                ExpectedN      = true,
            };
            test.Execute_And_Verify(AddrMode.I);
        }

        [Fact]
        public void CMP_I_Clears_N_If_A_Minus_Value_Is_Positive()
        {
            var test = new TestSpec()
            {
                A              = 0x02,
                OpCode         = OpCodeId.CMP_I,
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
        /// in the CMP_I instruction above, and are used by all addressing modes.
        /// </summary>
        [Fact]
        public void CMP_ZP_Works()
        {
            var test = new TestSpec()
            {
                A              = 0x33,
                OpCode         = OpCodeId.CMP_ZP,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 3,
            };
            test.Execute_And_Verify(AddrMode.ZP);
        }

        [Fact]
        public void CMP_ZP_X_Works()
        {
            var test = new TestSpec()
            {
                A              = 0x33,
                OpCode         = OpCodeId.CMP_ZP_X,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ZP_X);
        }


        /// <summary>
        /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
        /// </summary>
        [Fact]
        public void CMP_ZP_X_Where_ZeroPage_Address_Plus_X_Wraps_Over_Byte_Size_Works()
        {
            var test = new TestSpec()
            {
                A              = 0x33,
                OpCode         = OpCodeId.CMP_ZP_X,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ZP_X, ZP_X_Should_Wrap_Over_Byte: true);
        }

        [Fact]
        public void CMP_ABS_Works()
        {
            var test = new TestSpec()
            {
                A              = 0x33,
                OpCode         = OpCodeId.CMP_ABS,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ABS);
        }

        [Fact]
        public void CMP_ABS_X_Works()
        {
            var test = new TestSpec()
            {
                A              = 0x33,
                OpCode         = OpCodeId.CMP_ABS_X,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ABS_X);
        }

        [Fact]
        public void CMP_ABS_X_Where_Address_Plus_X_Crosses_Page_Boundary_Works()
        {
            var test = new TestSpec()
            {
                A              = 0x33,
                OpCode         = OpCodeId.CMP_ABS_X,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 5
            };
            test.Execute_And_Verify(AddrMode.ABS_X, FullAddress_Should_Cross_Page_Boundary: true);
        }

        [Fact]
        public void CMP_ABS_Y_Works()
        {
            var test = new TestSpec()
            {
                A              = 0x33,
                OpCode         = OpCodeId.CMP_ABS_Y,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 4
            };
            test.Execute_And_Verify(AddrMode.ABS_Y);
        }

        [Fact]
        public void CMP_ABS_Y_Where_Address_Plus_Y_Crosses_Page_Boundary_Works()
        {
            var test = new TestSpec()
            {
                A              = 0x33,
                OpCode         = OpCodeId.CMP_ABS_Y,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 5
            };
            test.Execute_And_Verify(AddrMode.ABS_Y, FullAddress_Should_Cross_Page_Boundary: true);
        }

        /// <summary>
        /// This mode is only used with the X register.
        /// Consider a situation where the instruction is
        ///    SOME_INSTRUCTION ($20,X)
        ///  X contains $04, and memory at $24 contains 74 20.
        /// First, X is added to $20 to get $24.
        /// The target address will be fetched from $24 resulting in a target address of $2074.
        /// The final value to use in the instruction will be at memory $2074.
        /// 
        /// If X + the immediate byte will wrap around to a zero-page address. 
        /// So you could code that like targetAddress = (X + opcode[1]) & 0xFF 
        /// 
        /// Indexed Indirect instructions are 2 bytes - the second byte is the zero-page address - $20 in the example. Obviously the fetched address has to be stored in the zero page.
        /// </summary>
        [Fact]
        public void CMP_IX_IND_Works()
        {
            var test = new TestSpec()
            {
                A              = 0x33,
                OpCode         = OpCodeId.CMP_IX_IND,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 6  // Should take 6 cycles, even though we are not 
            };
            test.Execute_And_Verify(AddrMode.IX_IND, FullAddress_Should_Cross_Page_Boundary: false);
        }
        
        /// <summary>
        /// Same as _IX_IND test above, but test zero-page wrap around if zero page address + X > 0xff
        /// </summary>
        [Fact]
        public void CMP_IX_IND_When_ZeroPage_Address_Plus_X_Wraps_Around_Works()
        {
            var test = new TestSpec()
            {
                A              = 0x33,
                OpCode         = OpCodeId.CMP_IX_IND,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 6
            };
            test.Execute_And_Verify(AddrMode.IX_IND, ZP_X_Should_Wrap_Over_Byte: true);
        }


        /// <summary>
        /// This mode is only used with the Y register. 
        /// 
        /// It differs in the order that Y is applied to the indirectly fetched address. 
        /// An example instruction that uses indirect index addressing is 
        ///    SOME_INSTRUCTION ($86),Y
        /// To calculate the target address, the CPU will first fetch the address stored at zero page location $86. 
        /// That address will be added to register Y to get the final target address. 
        /// For SOME_INSTRUCTION ($86),Y, if the address stored at $86 is $4028 (memory is 0086: 28 40, remember little endian) 
        /// and register Y contains $10, then the final target address would be $4038. 
        /// 
        /// The final value to be used with the instruction is at memory $4038.
        /// 
        /// Indirect Indexed instructions are 2 bytes - the second byte is the zero-page address - $86 in the example. (So the fetched address has to be stored in the zero page.)
        /// 
        /// While indexed indirect addressing will only generate a zero-page address, this mode's target address is not wrapped - it can be anywhere in the 16-bit address space.
        /// </summary>
        [Fact]
        public void CMP_IND_IX_Works()
        {
            var test = new TestSpec()
            {
                A              = 0x33,
                OpCode         = OpCodeId.CMP_IND_IX,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 5
            };
            test.Execute_And_Verify(AddrMode.IND_IX);    
        }

        /// <summary>
        /// Same as _IND_IX_ test above, 
        /// but tests if indirect address (read from zero page) + Y register crosses a page boundary.
        /// If so, an extra cycle is used.
        /// </summary>
        [Fact]
        public void CMP_IND_IX_When_ZeroPage_Address_Vector_Plus_Y_Crosses_Page_Works()
        {
            var test = new TestSpec()
            {
                A              = 0x33,
                OpCode         = OpCodeId.CMP_IND_IX,
                FinalValue     = 0x33,
                ExpectedC      = true,
                ExpectedZ      = true,
                ExpectedN      = false,
                ExpectedCycles = 6
            };
            test.Execute_And_Verify(AddrMode.IND_IX, FullAddress_Should_Cross_Page_Boundary: true);
        }
    }
}
