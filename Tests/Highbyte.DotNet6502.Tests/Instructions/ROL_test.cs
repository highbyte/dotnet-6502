namespace Highbyte.DotNet6502.Tests.Instructions;

public class ROL_test
{
    [Fact]
    public void ROL_ACC_Takes_2_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.ROL_ACC,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.Accumulator);
    }

    [Fact]
    public void ROL_ACC_Does_Correct_Logic_Operation()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0b10011001,
            OpCode        = OpCodeId.ROL_ACC,
            ExpectedA      = 0b00110010,
            ExpectedC      = true
        };
        test.Execute_And_Verify(AddrMode.Accumulator);
    }

    [Fact]
    public void ROL_ACC_Does_Correct_Logic_Operation2()
    {
        var test = new TestSpec
        {
            C              = true,
            A              = 0b00011001,
            OpCode        = OpCodeId.ROL_ACC,
            ExpectedA      = 0b00110011,        // Bit 0 is set to the original value of carry
            ExpectedC      = false
        };
        test.Execute_And_Verify(AddrMode.Accumulator);
    }

    [Fact]
    public void ROL_ACC_Sets_Zero_Flag_If_Result_Is_Zero()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0b10000000,
            OpCode         = OpCodeId.ROL_ACC,
            ExpectedA      = 0b00000000,
            ExpectedZ      = true
        };
        test.Execute_And_Verify(AddrMode.Accumulator);
    }

    [Fact]
    public void ROL_ACC_Clears_Zero_Flag_If_Result_Is_Not_Zero()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0b10000001,
            OpCode         = OpCodeId.ROL_ACC,
            ExpectedA      = 0b00000010,
            ExpectedZ      = false
        };
        test.Execute_And_Verify(AddrMode.Accumulator);
    }

    [Fact]
    public void ROL_ACC_Sets_Negative_Flag_If_Result_Is_Negative()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0b01000001,
            OpCode         = OpCodeId.ROL_ACC,
            ExpectedA      = 0b10000010,
            ExpectedN      = true
        };
        test.Execute_And_Verify(AddrMode.Accumulator);
    }
    
    [Fact]
    public void ROL_ACC_Clears_Negative_Flag_If_Result_Is_Not_Negative()
    {
        var test = new TestSpec
        {
            C              = false,
            A              = 0b00100001,
            OpCode         = OpCodeId.ROL_ACC,
            ExpectedA      = 0b01000010,
            ExpectedN      = false
        };
        test.Execute_And_Verify(AddrMode.Accumulator);
    }  

    // ----------------------------------------------------------------------------------------
    // Other addressing modes than _Acc
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// Only test addressing mode works, the binary operation where tested
    /// in the ROL_I instruction above, and are used by all addressing modes.
    /// </summary>
    [Fact]
    public void ROL_ZP_Works()
    {
        var test = new TestSpec()
        {
            C              = false,
            OpCode         = OpCodeId.ROL_ZP,
            InsEffect      = InstrEffect.Mem,
            FinalValue     = 0b10011001,
            ExpectedMemVal = 0b00110010,
            ExpectedCycles = 5
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void ROL_ZP_X_Works()
    {
        var test = new TestSpec()
        {
            C              = false,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.ROL_ZP_X,
            FinalValue     = 0b10011001,
            ExpectedMemVal = 0b00110010,
            ExpectedCycles = 6
        };
        test.Execute_And_Verify(AddrMode.ZP_X);
    }

    /// <summary>
    /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
    /// </summary>
    [Fact]
    public void ROL_ZP_X_Where_ZeroPage_Address_Plus_X_Wraps_Over_Byte_Size_Works()
    {
        var test = new TestSpec()
        {
            C              = false,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.ROL_ZP_X,
            FinalValue     = 0b10011001,
            ExpectedMemVal = 0b00110010,
            ExpectedCycles = 6
        };
        test.Execute_And_Verify(AddrMode.ZP_X, ZP_X_Should_Wrap_Over_Byte: true);
    }

    [Fact]
    public void ROL_ABS_Works()
    {
        var test = new TestSpec()
        {
            C              = false,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.ROL_ABS,
            FinalValue     = 0b10011001,
            ExpectedMemVal = 0b00110010,
            ExpectedCycles = 6
        };
        test.Execute_And_Verify(AddrMode.ABS);
    }

    [Fact]
    public void ROL_ABS_X_Works()
    {
        var test = new TestSpec()
        {
            C              = false,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.ROL_ABS_X,
            FinalValue     = 0b10011001,
            ExpectedMemVal = 0b00110010,
            ExpectedCycles = 7
        };
        test.Execute_And_Verify(AddrMode.ABS_X);
    }

    [Fact]
    public void ROL_ABS_X_Where_Address_Plus_X_Crosses_Page_Boundary_Works()
    {
        var test = new TestSpec()
        {
            C              = false,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.ROL_ABS_X,
            FinalValue     = 0b10011001,
            ExpectedMemVal = 0b00110010,
            ExpectedCycles = 7
        };
        test.Execute_And_Verify(AddrMode.ABS_X, FullAddress_Should_Cross_Page_Boundary: true);
    }
}
