namespace Highbyte.DotNet6502.Tests.Instructions;

public class ISC_test
{
    [Fact]
    public void ISC_ZP_Takes_5_Cycles()
    {
        var test = new TestSpec
        {
            A = 0x20, C = true,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.ISC_ZP,
            FinalValue     = 0x0F,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void ISC_ZP_Increments_Memory_Value()
    {
        var test = new TestSpec
        {
            A = 0x20, C = true,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.ISC_ZP,
            FinalValue     = 0x0F,
            ExpectedMemVal = 0x10,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void ISC_ZP_Subtracts_Incremented_Value_From_A()
    {
        // A=0x20, C=1 (no borrow), mem=0x0F → mem=0x10, SBC(0x20, 0x10, C=1): A=0x10
        var test = new TestSpec
        {
            A = 0x20, C = true,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.ISC_ZP,
            FinalValue     = 0x0F,
            ExpectedA      = 0x10,
            ExpectedC      = true,
            ExpectedZ      = false,
            ExpectedN      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void ISC_ZP_Sets_Zero_Flag_When_Result_Is_Zero()
    {
        // A=0x10, C=1, mem=0x0F → mem=0x10, SBC(0x10, 0x10, C=1): A=0x00, Z=1
        var test = new TestSpec
        {
            A = 0x10, C = true,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.ISC_ZP,
            FinalValue     = 0x0F,
            ExpectedA      = 0x00,
            ExpectedZ      = true,
            ExpectedC      = true,
            ExpectedN      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void ISC_ZP_Sets_Negative_Flag_When_Result_Is_Negative()
    {
        // A=0x05, C=1, mem=0x0F → mem=0x10, SBC(0x05, 0x10, C=1): 0x05-0x10=-11 (0xF5), N=1, C=0 (borrow)
        var test = new TestSpec
        {
            A = 0x05, C = true,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.ISC_ZP,
            FinalValue     = 0x0F,
            ExpectedA      = 0xF5,
            ExpectedN      = true,
            ExpectedC      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void ISC_ZP_X_Works()
    {
        var test = new TestSpec
        {
            A = 0x20, C = true,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.ISC_ZP_X,
            FinalValue     = 0x0F,
            ExpectedMemVal = 0x10,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.ZP_X);
    }

    [Fact]
    public void ISC_ABS_Works()
    {
        var test = new TestSpec
        {
            A = 0x20, C = true,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.ISC_ABS,
            FinalValue     = 0x0F,
            ExpectedMemVal = 0x10,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.ABS);
    }

    [Fact]
    public void ISC_ABS_X_Works()
    {
        var test = new TestSpec
        {
            A = 0x20, C = true,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.ISC_ABS_X,
            FinalValue     = 0x0F,
            ExpectedMemVal = 0x10,
            ExpectedCycles = 7,
        };
        test.Execute_And_Verify(AddrMode.ABS_X);
    }

    [Fact]
    public void ISC_ABS_Y_Works()
    {
        var test = new TestSpec
        {
            A = 0x20, C = true,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.ISC_ABS_Y,
            FinalValue     = 0x0F,
            ExpectedMemVal = 0x10,
            ExpectedCycles = 7,
        };
        test.Execute_And_Verify(AddrMode.ABS_Y);
    }

    [Fact]
    public void ISC_IX_IND_Works()
    {
        var test = new TestSpec
        {
            A = 0x20, C = true,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.ISC_IX_IND,
            FinalValue     = 0x0F,
            ExpectedMemVal = 0x10,
            ExpectedCycles = 8,
        };
        test.Execute_And_Verify(AddrMode.IX_IND);
    }

    [Fact]
    public void ISC_IND_IX_Works()
    {
        var test = new TestSpec
        {
            A = 0x20, C = true,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.ISC_IND_IX,
            FinalValue     = 0x0F,
            ExpectedMemVal = 0x10,
            ExpectedCycles = 8,
        };
        test.Execute_And_Verify(AddrMode.IND_IX);
    }
}
