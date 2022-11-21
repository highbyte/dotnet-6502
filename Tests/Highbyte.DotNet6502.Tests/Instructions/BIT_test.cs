using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions;

public class BIT_test
{
    [Fact]
    public void BIT_I_Takes_3_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.BIT_ZP,
            FinalValue     = 0,
            ExpectedCycles = 3,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void BIT_I_Does_Correct_Logic_Operation()
    {
        var test = new TestSpec
        {
            InsEffect      = InstrEffect.StatusOnly,
            OpCode         = OpCodeId.BIT_ZP,
            A              = 0b01001100,
            FinalValue     = 0b11010000, // The contents of the memory that is tested
            ExpectedA      = 0b01001100, // A should be unchanged
            ExpectedMemVal = 0b11010000, // Memory should be unchanged
            ExpectedZ      = false,      // Zero flag is not set because A AND (bitwise) Memory is 0
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void BIT_I_Does_Correct_Logic_Operation2()
    {
        var test = new TestSpec
        {
            InsEffect      = InstrEffect.StatusOnly,
            OpCode         = OpCodeId.BIT_ZP,
            A              = 0b00000000,
            FinalValue     = 0b11111111,
            ExpectedA      = 0b00000000,
            ExpectedMemVal = 0b11111111,
            ExpectedZ      = true           // Zero flag is set because A AND (bitwise) Memory is 0
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void BIT_I_Sets_N_If_Memory_Address_Is_Negative()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.StatusOnly,
            OpCode         = OpCodeId.BIT_ZP,
            A              = 0b00000000,    // A is not used in evaluation of N flag
            FinalValue     = 0b11111111,
            ExpectedN      = true,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void BIT_I_Clears_N_If_Memory_Address_Is_Positive()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.StatusOnly,
            OpCode         = OpCodeId.BIT_ZP,
            A              = 0b10000000,    // A is not used in evaluation of N flag
            FinalValue     = 0b01111111,
            ExpectedN      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void BIT_I_Sets_V_If_Memory_Address_Has_Bit_6_Set()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.StatusOnly,
            OpCode         = OpCodeId.BIT_ZP,
            A              = 0b00000000,    // A is not used in evaluation of V flag
            FinalValue     = 0b01000001,
            ExpectedV      = true,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void BIT_I_Clears_V_If_Memory_Address_Has_Bit_6_Clear()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.StatusOnly,
            OpCode         = OpCodeId.BIT_ZP,
            A              = 0b00100000,    // A is not used in evaluation of V flag
            FinalValue     = 0b00000001,
            ExpectedV      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }


    // ----------------------------------------------------------------------------------------
    // Other addressing modes than _ZP
    // ----------------------------------------------------------------------------------------

    [Fact]
    public void BIT_ABS_Works()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.StatusOnly,
            OpCode         = OpCodeId.BIT_ABS,
            A              = 0b01001100,
            FinalValue     = 0b11010000, // The contents of the memory that is tested
            ExpectedA      = 0b01001100, // A should be unchanged
            ExpectedMemVal = 0b11110000, // Memory should be unchanged
            ExpectedZ      = false,      // Zero flag is not set because A AND (bitwise) Memory is 0
            ExpectedN      = true,       // Negative flag is set because Memory bit 7 is set
            ExpectedV      = true,       // Overflow flag is set because Memory bit 7 is set
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS);
    }
}
