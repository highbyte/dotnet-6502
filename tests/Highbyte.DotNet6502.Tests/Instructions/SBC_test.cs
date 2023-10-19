namespace Highbyte.DotNet6502.Tests.Instructions;

public class SBC_test
{
    [Fact]
    public void SBC_I_Takes_2_Cycles()
    {
        var test = new TestSpec
        {
            OpCode          = OpCodeId.SBC_I,
            FinalValue      = 0x01,
            ExpectedCycles  = 2,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Zero_Minus_Zero_Should_Be_Zero_With_Carry_Set()
    {
        var test = new TestSpec
        {
            C              = true,  // Carry must be set before SBC to perform a subtraction without borrow.
            A              = 0x00,
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x00,
            ExpectedA      = 0x00,
            ExpectedC      = true,  // Carry should still be set after instruction
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Zero_Minus_Zero_Should_Be_Minus_One_With_Carry_Clear()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x00,
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x00,
            ExpectedA      = 0xff,
            ExpectedC      = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }


    [Fact]
    public void SBC_I_Zero_Minus_Minus_One_Should_Be_Zero_If_Initial_Carry_Is_Clear()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x00,
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0xff,
            ExpectedA      = 0x00,   // Will result in 0 and not 1 (because of C clear at beginning?)
            ExpectedC      = false,  // Carry should still be clear after instruction
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Zero_Minus_Minus_One_Should_Be_One_If_Initial_Carry_Is_Set()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x00,
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0xff,
            ExpectedA      = 0x01,   // Will result in 1 (correct because Carry was set before)
            ExpectedC      = false,  // Carry should still be clear after instruction
        };
        test.Execute_And_Verify(AddrMode.I);
    }        

    [Fact]
    public void SBC_I_Two_Small_Unsigned_Numbers()
    {
        var test = new TestSpec
        {
            C              = true,  // Carry must be set before SBC to perform a subtraction without borrow.
            A              = 0x03,
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Two_Small_Signed_Numbers_With_Different_Signs()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02,
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0xff, // -1
            ExpectedA      = 0x03,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Two_Small_Signed_Negative_Numbers()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0xff, // -1
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0xfe, // -2
            ExpectedA      = 0x01,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Two_Zero_Numbers_Will_Get_Zero_Flag_Set()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x00,
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x00,
            ExpectedA      = 0x00,
            ExpectedZ      = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Result_Is_Less_Than_Zero_Will_Get_Zero_Flag_Clear()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x00,
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x01,
            ExpectedZ      = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Result_Is_Greater_Than_Zero_Will_Get_Zero_Flag_Clear()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x00,
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0xff, // -1
            ExpectedZ      = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Result_Is_Has_Bit_7_Set_Will_Get_Negative_Flag_Set()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x00,
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x01,
            ExpectedN      = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    // ----------------------------------------------------------------------------------------
    // Carry tests
    // ----------------------------------------------------------------------------------------

    [Fact]
    public void SBC_I_With_Two_Unsigned_Values_With_Operand_Positive_Resulting_In_Positive_Value_Get_Carry_Flag_Set()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x20, // 32
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x10, // 16
            ExpectedA      = 0x10, // 16
            ExpectedC      = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_With_Two_Unsigned_Values_With_Operand_Positive_Resulting_In_Negative_Value_Get_Carry_Flag_Clear()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x10, // 16
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x20, // 32
            ExpectedA      = 0xf0, // -16
            ExpectedC      = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_With_Two_Unsigned_Values_With_Operand_Positive_Resulting_In_Zero_Value_Get_Carry_Flag_Set()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x10, // 16
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x10, // 32
            ExpectedA      = 0x00, // -16
            ExpectedC      = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_With_Two_Signed_Values_With_Operand_Negative_Resulting_In_Positive_Value_Get_Carry_Flag_Clear()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x10, // 16
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0xff, // -1
            ExpectedA      = 0x11, // 17
            ExpectedC      = false, // Because subtracting a neg nr? Compare with #$20-#$10 which get Carry 1
        };
        test.Execute_And_Verify(AddrMode.I);            
    }

    [Fact]
    public void SBC_I_With_Two_Signed_Values_With_Both_Register_And_Operand_Negative_Resulting_In_Negative_Value_Get_Carry_Flag_Clear()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0xf0, // -16
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0xff, // -1
            ExpectedA      = 0xf1, // -15
            ExpectedC      = false, // Because subtracting a neg nr? compare with #$20-#$10 which get Carry 1
        };
        test.Execute_And_Verify(AddrMode.I);                  
    }

    [Fact]
    public void SBC_I_With_Two_Signed_Values_With_Both_Register_And_Operand_Negative_Resulting_In_Positive_Value_Get_Carry_Flag_Set()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0xf0, // -16
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0xef, // -17
            ExpectedA      = 0x01, // 1
            ExpectedC      = true, //  Event though subtracting a neg nr, result is positive, so still Carry 1
        };
        test.Execute_And_Verify(AddrMode.I);                  
    }


    [Fact]
    public void SBC_I_With_Two_Signed_Values_With_Register_Negative_And_Operand_Positive_Resulting_In_Negative_Value_Get_Carry_Flag_Set()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0xf0, // -16
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x01, // 1
            ExpectedA      = 0xef, // -17
            ExpectedC      = true, // Because even though result in negative, the A and operand have different signs?
        };
        test.Execute_And_Verify(AddrMode.I);        
    }

    // ----------------------------------------------------------------------------------------
    // Overflow tests
    // ----------------------------------------------------------------------------------------

    [Fact]
    public void SBC_I_With_Two_Positive_Signed_Values_Resulting_In_A_Positive_Value_Will_Clear_Overflow_Flag()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02, // 2
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x01, // 1
            ExpectedV      = false,
        };
        test.Execute_And_Verify(AddrMode.I);        
    }

    [Fact]
    public void SBC_I_Signed_Value_And_Difference_Is_Negative_And_Lower_Than_Minus_128_Set_Overflow_Flag()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0xfe, // -2
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x7f, // 127
            // -2 -127 = -129, which is an invalid signed number (minimum -128), therefor the result is not correct.
            // Result will be 0x7f, which is not correct, and therefor the Overflow flag is set.
            ExpectedV      = true,
        };
        test.Execute_And_Verify(AddrMode.I);               
    }

    [Fact]
    public void SBC_I_With_Signed_Value_And_Difference_Is_Postive_And_Greater_Than_127_Will_Set_Overflow_Flag()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x01, // 1
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x81, // -127
            // 1 -(-127) = 128, which is an invalid signed number (maximum 127), therefor the result is not correct.
            ExpectedV      = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Signed_Value_And_Difference_Is_Negative_And_Greater_Than_Or_Equal_To_Minus_128_Clears_Overflow_Flag()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0xfe, // -2
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x7e, // 126
            // -2 -126 = -128, is a valid signed number (minimum -128)
            // Therefor the Overflow flag is clear.
            ExpectedV      = false,
        };
        test.Execute_And_Verify(AddrMode.I);            
    }


    [Fact]
    public void SBC_I_With_Signed_Value_And_Difference_Is_Postive_And_Less_Than_Or_Equal_To_127_Will_Clears_Overflow_Flag()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x01, // 1
            OpCode         = OpCodeId.SBC_I,
            FinalValue     = 0x82, // -126
            // 1 -(-126) = 127, which is an valid signed number (maximum 127).
            // And therefor the Overflow flag is clear.
            ExpectedV      = false,
        };
        test.Execute_And_Verify(AddrMode.I); 
    }

    // ----------------------------------------------------------------------------------------
    // Other addressing modes than SBC_I
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// Only test addressing mode works, the binary arithmetic (carry, zero, overflow flags) where tested
    /// in the SBC_I instruction above, and are used by all addressing modes.
    /// </summary>
    [Fact]
    public void SBC_ZP_Works()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02,
            OpCode         = OpCodeId.SBC_ZP,
            FinalValue     = 0x01,
            ExpectedA      = 0x01,
            ExpectedCycles = 3,
        };
        test.Execute_And_Verify(AddrMode.ZP);               
    }

    [Fact]
    public void SBC_ZP_X_Works()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02,
            OpCode         = OpCodeId.SBC_ZP_X,
            FinalValue     = 0x01,
            ExpectedA      = 0x01,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ZP_X);   
    }

    /// <summary>
    /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
    /// If we repeat the last example but with $FF in the X register then the accumulator will be stored to memory $007F (e.g. $80 + $FF => $7F) and not $017F.
    /// </summary>
    [Fact]
    public void SBC_ZP_X_Where_ZeroPage_Address_Plus_X_Wraps_Over_Byte_Size_Works()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02,
            OpCode         = OpCodeId.SBC_ZP_X,
            FinalValue     = 0x01,
            ExpectedA      = 0x01,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ZP_X, ZP_X_Should_Wrap_Over_Byte: true);
    }

    /// <summary>
    /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
    /// If we repeat the last example but with $FF in the X register then the accumulator will be stored to memory $007F (e.g. $80 + $FF => $7F) and not $017F.
    /// </summary>
    [Fact]
    public void SBC_ABS_Works()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02,
            OpCode         = OpCodeId.SBC_ABS,
            FinalValue     = 0x01,
            ExpectedA      = 0x01,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS);
    }

    [Fact]
    public void SBC_ABS_X_Works()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02,
            OpCode         = OpCodeId.SBC_ABS_X,
            FinalValue     = 0x01,
            ExpectedA      = 0x01,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS_X);
    }

    /// <summary>
    /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
    /// </summary>
    [Fact]
    public void ABS_X_Where_Address_Plus_X_Crosses_Page_Boundary_Works()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02,
            OpCode         = OpCodeId.SBC_ABS_X,
            FinalValue     = 0x01,
            ExpectedA      = 0x01,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ABS_X, FullAddress_Should_Cross_Page_Boundary: true);
    }

    [Fact]
    public void SBC_ABS_Y_Works()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02,
            OpCode         = OpCodeId.SBC_ABS_Y,
            FinalValue     = 0x01,
            ExpectedA      = 0x01,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS_Y);
    }

    /// <summary>
    /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
    /// </summary>
    [Fact]
    public void SBC_ABS_Y_Where_Address_Plus_X_Crosses_Page_Boundary_Works()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02,
            OpCode         = OpCodeId.SBC_ABS_Y,
            FinalValue     = 0x01,
            ExpectedA      = 0x01,
            ExpectedCycles = 5,
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
    public void SBC_IX_IND_Works()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02,
            OpCode         = OpCodeId.SBC_IX_IND,
            FinalValue     = 0x01,
            ExpectedA      = 0x01,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.IX_IND);
    }

    /// <summary>
    /// Same as _IX_IND test above, but test zero-page wrap around if zero page address + X > 0xff
    /// </summary>
    [Fact]
    public void SBC_IX_IND_When_ZeroPage_Address_Plus_X_Wraps_Around_Works()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02,
            OpCode         = OpCodeId.SBC_IX_IND,
            FinalValue     = 0x01,
            ExpectedA      = 0x01,
            ExpectedCycles = 6,
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
    public void SBC_IND_IX_Works()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02,
            OpCode         = OpCodeId.SBC_IND_IX,
            FinalValue     = 0x01,
            ExpectedA      = 0x01,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.IND_IX);
    }

    /// <summary>
    /// Same as _IND_IX test above, 
    /// but tests if indirect address (read from zero page) + Y register crosses a page boundary.
    /// If so, an extra cycle is used.
    /// </summary>
    [Fact]
    public void SBC_IND_IX_When_ZeroPage_Address_Vector_Plus_Y_Crosses_Page_Works()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0x02,
            OpCode         = OpCodeId.SBC_IND_IX,
            FinalValue     = 0x01,
            ExpectedA      = 0x01,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.IND_IX, FullAddress_Should_Cross_Page_Boundary: true);
    }

    [Fact]
    public void SBC_I_Decimal_Mode_Can_Subtract_8_from_10()
    {
        var test = new TestSpec
        {
            D = true,
            C = true,  // Carry must be set before SBC to perform a subtraction without borrow.
            A = 0x10,
            OpCode = OpCodeId.SBC_I,
            FinalValue = 0x08,
            ExpectedA = 0x02,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Decimal_Mode_Can_Subtract_11_from_96()
    {
        var test = new TestSpec
        {
            D = true,
            C = true,  // Carry must be set before SBC to perform a subtraction without borrow.
            A = 0x96,
            OpCode = OpCodeId.SBC_I,
            FinalValue = 0x11,
            ExpectedA = 0x85,
            ExpectedC = true,
            ExpectedZ = false,
            ExpectedN = true,
            ExpectedV = false
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Decimal_Mode_Can_Subtract_11_from_5()
    {
        var test = new TestSpec
        {
            D = true,
            C = true,  // Carry must be set before SBC to perform a subtraction without borrow.
            A = 0x5,
            OpCode = OpCodeId.SBC_I,
            FinalValue = 0x11,
            ExpectedA = 0x94,   // Wraps around 0 back to 99
            ExpectedC = false,
            ExpectedZ = false,
            ExpectedN = true,
            ExpectedV = false
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Decimal_Mode_Can_Subtract_0_from_0()
    {
        var test = new TestSpec
        {
            D = true,
            C = true,  // Carry must be set before SBC to perform a subtraction without borrow.
            A = 0x0,
            OpCode = OpCodeId.SBC_I,
            FinalValue = 0x0,
            ExpectedA = 0x0,
            ExpectedC = true,
            ExpectedZ = true,
            ExpectedN = false,
            ExpectedV = false
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void SBC_I_Decimal_Mode_Can_Subtract_81_from_1()
    {
        var test = new TestSpec
        {
            D = true,
            C = true,  // Carry must be set before SBC to perform a subtraction without borrow.
            A = 0x1,
            OpCode = OpCodeId.SBC_I,
            FinalValue = 0x81,
            ExpectedA = 0x20,   // Wraps around 0 back to 99
            ExpectedC = false,
            ExpectedZ = false,
            ExpectedN = true,
            ExpectedV = true
        };
        test.Execute_And_Verify(AddrMode.I);
    }
}
