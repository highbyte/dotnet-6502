using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions;

public class DEC_test
{

    [Fact]
    public void DEC_ZP_Takes_5_Cycles()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DEC_ZP,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void DEC_ZP_Decreases_Value_At_Memory_Location()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DEC_ZP,
            FinalValue     = 0x02,  // Will get written to the memory location being changed before instruction runs. Default address is used if not specified in ZeroPageAddress
            //ZeroPageAddress = 0x20  // Optional. Will use a default ZeroPage address for test if not set
            ExpectedMemVal = 0x01,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void DEC_ZP_Sets_Zero_Flag_If_Result_Is_Zero()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DEC_ZP,
            FinalValue     = 0x01,
            ExpectedMemVal = 0x00,
            ExpectedZ      = true,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void DEC_ZP_Clears_Zero_Flag_If_Result_Is_Not_Zero()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DEC_ZP,
            FinalValue     = 0x02,
            ExpectedMemVal = 0x01,
            ExpectedZ      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void DEC_ZP_Sets_Negative_Flag_If_Result_Is_Negative()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DEC_ZP,
            FinalValue     = 0x00,
            ExpectedMemVal = 0xff,
            ExpectedN      = true,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void DEC_ZP_Clears_Negative_Flag_If_Result_Is_Positive()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DEC_ZP,
            FinalValue     = 0x02,
            ExpectedMemVal = 0x01,
            ExpectedN      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    // ----------------------------------------------------------------------------------------
    // Other addressing modes than _ZP
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// Only test addressing mode works, the binary operation where tested
    /// in the DEC_ZP instruction above, and are used by all addressing modes.
    /// </summary>
    [Fact]
    public void DEC_ZP_X_Works()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DEC_ZP_X,
            FinalValue     = 0x02,
            ExpectedMemVal = 0x01,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.ZP_X);
    }


    /// <summary>
    /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
    /// If we repeat the last example but with $FF in the X register then memory that will be increased is $007F (e.g. $80 + $FF => $7F) and not $017F.
    /// </summary>
    [Fact]
    public void DEC_ZP_X_Where_ZeroPage_Address_Plus_X_Wraps_Over_Byte_Size_Works()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DEC_ZP_X,
            FinalValue     = 0x02,
            ExpectedMemVal = 0x01,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.ZP_X, ZP_X_Should_Wrap_Over_Byte: true);
    }

    [Fact]
    public void DEC_ABS_Works()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DEC_ABS,
            FinalValue     = 0x02,
            ExpectedMemVal = 0x01,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.ABS);
    }

    [Fact]
    public void DEC_ABS_X_Works()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DEC_ABS_X,
            FinalValue     = 0x02,
            ExpectedMemVal = 0x01,
            ExpectedCycles = 7,
        };
        test.Execute_And_Verify(AddrMode.ABS_X);
    }
}
