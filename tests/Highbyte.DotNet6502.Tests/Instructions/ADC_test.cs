namespace Highbyte.DotNet6502.Tests.Instructions;

public class ADC_test
{

    [Fact]
    public void ADC_Takes_2_Cycles()
    {
        var test = new TestSpec
        {
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0x01,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_Can_Add_0_Plus_0()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x00,
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0x00,
            ExpectedA      = 0x00,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_Can_Add_1_Plus_1()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_Can_Add_2_Plus_Negative_1()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x02,
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0xff,
            ExpectedA      = 0x01,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_Can_Add_Negative_One_Plus_Negative_2()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0xff, // -1
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0xfe, // -2
            ExpectedA      = 0xfd, // -3
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_Sets_Zero_Flag_If_Result_Is_Zero()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x00,
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0x00, 
            ExpectedZ      = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_Clears_Zero_Flag_If_Result_Is_Positive()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x00,
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0x01, 
            ExpectedA      = 0x01,
            ExpectedZ      = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_Clears_Zero_Flag_If_Result_Is_Negative()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x00,
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0xff,    // -1
            ExpectedZ      = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_Sets_Negative_Flag_If_Result_Is_Negative()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x00,
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0xff, // -1
            ExpectedN      = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_Clears_Negative_Flag_If_Result_Is_Positive()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x00,
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0x01,
            ExpectedN      = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    // ----------------------------------------------------------------------------------------
    // Carry tests
    // ----------------------------------------------------------------------------------------

    [Fact]
    public void ADC_Sets_Carry_Flag_If_The_Sum_Of_Two_Unsigned_Values_Exceeds_255()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x10,
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0xff, // 255
            ExpectedA      = 0x0f, // 15   (wrap around byte limit) 
            ExpectedC      = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_Clears_Carry_Flag_If_Sum_Of_Two_Signed_Values_Is_Negative_But_Higher_Or_Equal_To_Negative_128()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0x81, // -127
            ExpectedA      = 0x82, // -126
            ExpectedC      = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }    

    [Fact]
    public void ADC_Sets_Carry_Flag_If_Sum_Of_Two_Signed_Values_Is_Negative_But_Lower_Than_Negative_128()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0xff, // -1
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0x80, // -128
            ExpectedA      = 0x7f, // Sum will be lower than limit -128, and instead become +127
            ExpectedC      = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_Sets_Carry_Flag_If_Sum_Of_Two_Signed_Values_Is_Positive_But_Lower_Than_Or_Equal_To_127()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x10,
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0xff, // -1  (which could be 255 if this was considered a signed number)
            ExpectedC      = true, // Sum would be 0x0f if considered signed. But would be >255 if considered unsigned, therefore the carry is set.
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_Clears_Carry_Flag_If_Sum_Of_Two_Signed_Values_Higher_Than_127()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0x7f,   // 127 
            ExpectedC      = false,  // Sum would 0x80, which is -128 (within limit) if considered signed. And is 128 if considered unsigned (also within limit)
        };
        test.Execute_And_Verify(AddrMode.I);
    }        

    // ----------------------------------------------------------------------------------------
    // Overflow tests.
    // Overflow flags is set if the sign bit is "incorrect".
    // ----------------------------------------------------------------------------------------

    [Fact]
    public void ADC_Clears_Overflow_Flag_If_Two_Positive_Signed_Numbers_Results_In_A_Valid_Positive_Value()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0x01,
            ExpectedV      = false,
        };
        test.Execute_And_Verify(AddrMode.I);            
    }

    [Fact]
    public void ADC_Sets_Overflow_Flag_If_Two_Positive_Signed_Numbers_Results_In_Invalid_Positive_Value()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x7f, // 127
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0x01, 
            ExpectedV      = true, // Sum would have been 128 (invalid signed positive number) but wasn't, so Overflow.
        };
        test.Execute_And_Verify(AddrMode.I);            
    }

    [Fact]
    public void ADC_Clears_Overflow_Flag_If_Two_Negative_Signed_Numbers_Results_In_A_Valid_Negative_Value()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0xff, // -1
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0xff, // -1
            ExpectedV      = false,
        };
        test.Execute_And_Verify(AddrMode.I);            
    }

    [Fact]
    public void ADC_Sets_Overflow_Flag_If_Two_Negative_Signed_Numbers_Results_In_Invalid_Negative_Value()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x80, // -128
            OpCode         = OpCodeId.ADC_I,
            FinalValue     = 0xff, // -1
            ExpectedV      = true, // Sum would have been -129 (invalid), so wasn't. Thus Overflow.
        };
        test.Execute_And_Verify(AddrMode.I);            
    }

    // ----------------------------------------------------------------------------------------
    // Other addressing modes than _I
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// Only test addressing mode works, the binary arithmetic (carry, zero, overflow flags) where tested
    /// in the ADC_I instruction above, and are used by all addressing modes.
    /// </summary>
    [Fact]
    public void ADC_ZP_Works()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_ZP,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
            ExpectedCycles = 3,
        };
        test.Execute_And_Verify(AddrMode.ZP);               
    }

    [Fact]
    public void ADC_ZP_X_Works()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_ZP_X,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ZP_X);   
    }

    /// <summary>
    /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
    /// If we repeat the last example but with $FF in the X register then the accumulator will be stored to memory $007F (e.g. $80 + $FF => $7F) and not $017F.
    /// </summary>
    [Fact]
    public void ADC_ZP_X_Where_ZeroPage_Address_Plus_X_Wraps_Over_Byte_Size_Works()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_ZP_X,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ZP_X, ZP_X_Should_Wrap_Over_Byte: true);
    }

    /// <summary>
    /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
    /// If we repeat the last example but with $FF in the X register then the accumulator will be stored to memory $007F (e.g. $80 + $FF => $7F) and not $017F.
    /// </summary>
    [Fact]
    public void ADC_ABS_Works()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_ABS,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS);
    }

    [Fact]
    public void ADC_ABS_X_Works()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_ABS_X,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS_X);
    }

    /// <summary>
    /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
    /// </summary>
    [Fact]
    public void ADC_ABS_X_Where_Address_Plus_X_Crosses_Page_Boundary_Works()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_ABS_X,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ABS_X, FullAddress_Should_Cross_Page_Boundary: true);
    }

    [Fact]
    public void ADC_ABS_Y_Works()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_ABS_Y,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS_Y);
    }

    /// <summary>
    /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
    /// </summary>
    [Fact]
    public void ADC_ABS_Y_Where_Address_Plus_X_Crosses_Page_Boundary_Works()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_ABS_Y,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
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
    public void ADC_IX_IND_Works()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_IX_IND,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.IX_IND);
    }

    /// <summary>
    /// Same as _IX_IND test above, but test zero-page wrap around if zero page address + X > 0xff
    /// </summary>
    [Fact]
    public void ADC_IX_IND_When_ZeroPage_Address_Plus_X_Wraps_Around_Works()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_IX_IND,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
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
    public void ADC_IND_IX_Works()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_IND_IX,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
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
    public void ADC_IND_IX_When_ZeroPage_Address_Vector_Plus_Y_Crosses_Page_Works()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0x01,
            OpCode         = OpCodeId.ADC_IND_IX,
            FinalValue     = 0x01,
            ExpectedA      = 0x02,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.IND_IX, FullAddress_Should_Cross_Page_Boundary: true);
    }

    [Fact]
    public void ADC_DecimalMode_Can_Add_1_Plus_1()
    {
        var test = new TestSpec
        {
            D = true,
            C = false,
            A = 0x01,
            OpCode = OpCodeId.ADC_I,
            FinalValue = 0x01,
            ExpectedA = 0x02,
            ExpectedC = false,
            ExpectedZ = false,
            ExpectedV = false   // Overflow flag for binary values >= 127 (0x80)

        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_DecimalMode_Can_Add_72_Plus_10()
    {
        var test = new TestSpec
        {
            D = true,
            C = false,
            A = 0x72,
            OpCode = OpCodeId.ADC_I,
            FinalValue = 0x10,
            ExpectedA = 0x82,
            ExpectedC = false,
            ExpectedZ = false,
            ExpectedN = true,       // Negative flag is set because the result has bit 7 set (same as for binary mode)
            ExpectedV = true        // Overflow flag for binary values >= 127 (0x80)
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_DecimalMode_Can_Add_96_Plus_3()
    {
        var test = new TestSpec
        {
            D = true,
            C = false,
            A = 0x96,
            OpCode = OpCodeId.ADC_I,
            FinalValue = 0x03,
            ExpectedA = 0x99,
            ExpectedC = false,
            ExpectedZ = false,
            ExpectedN = true
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_DecimalMode_Can_Add_96_Plus_4_Which_Should_Be_0()
    {
        var test = new TestSpec
        {
            D = true,
            C = false,
            A = 0x96,
            OpCode = OpCodeId.ADC_I,
            FinalValue = 0x04,
            ExpectedA = 0x00,
            ExpectedC = true,
            ExpectedZ = false,       // Zero flag is set as if a binary add was done. That would not be a sum 0, thus false.
            ExpectedN = true,
            ExpectedV = false

        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ADC_DecimalMode_Can_Add_96_Plus_5_Which_Should_Be_1()
    {
        var test = new TestSpec
        {
            D = true,
            C = false,
            A = 0x96,
            OpCode = OpCodeId.ADC_I,
            FinalValue = 0x05,
            ExpectedA = 0x01,
            ExpectedC = true,
            ExpectedZ = false,
            ExpectedN = true,
            ExpectedV = false
        };
        test.Execute_And_Verify(AddrMode.I);
    }
}
