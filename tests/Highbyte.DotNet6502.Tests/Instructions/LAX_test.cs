namespace Highbyte.DotNet6502.Tests.Instructions;

public class LAX_test
{
    [Fact]
    public void LAX_ZP_Takes_3_Cycles()
    {
        var test = new TestSpec
        {
            A = 0, X = 0,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAX_ZP,
            FinalValue     = 0xAB,
            ExpectedCycles = 3,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void LAX_ZP_Loads_Both_A_And_X_With_Same_Value()
    {
        var test = new TestSpec
        {
            A = 0x00, X = 0x00,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAX_ZP,
            FinalValue     = 0xAB,
            ExpectedA      = 0xAB,
            ExpectedX      = 0xAB,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void LAX_ZP_Sets_Negative_Flag_When_Bit7_Set()
    {
        var test = new TestSpec
        {
            A = 0x00, X = 0x00,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAX_ZP,
            FinalValue     = 0x80,
            ExpectedA      = 0x80,
            ExpectedX      = 0x80,
            ExpectedN      = true,
            ExpectedZ      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void LAX_ZP_Sets_Zero_Flag_When_Value_Is_Zero()
    {
        var test = new TestSpec
        {
            A = 0x01, X = 0x01,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAX_ZP,
            FinalValue     = 0x00,
            ExpectedA      = 0x00,
            ExpectedX      = 0x00,
            ExpectedZ      = true,
            ExpectedN      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void LAX_ABS_Works()
    {
        var test = new TestSpec
        {
            A = 0x00, X = 0x00,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAX_ABS,
            FinalValue     = 0x42,
            ExpectedA      = 0x42,
            ExpectedX      = 0x42,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS);
    }

    [Fact]
    public void LAX_ABS_Y_Takes_4_Cycles()
    {
        var test = new TestSpec
        {
            A = 0x00, X = 0x00,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAX_ABS_Y,
            FinalValue     = 0x42,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS_Y);
    }

    [Fact]
    public void LAX_ABS_Y_Takes_5_Cycles_When_Page_Boundary_Crossed()
    {
        var test = new TestSpec
        {
            A = 0x00, X = 0x00,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAX_ABS_Y,
            FinalValue     = 0x42,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ABS_Y, fullAddress_Should_Cross_Page_Boundary: true);
    }

    [Fact]
    public void LAX_IX_IND_Works()
    {
        var test = new TestSpec
        {
            A = 0x00, X = 0x05,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAX_IX_IND,
            FinalValue     = 0x77,
            ExpectedA      = 0x77,
            ExpectedX      = 0x77,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.IX_IND);
    }

    [Fact]
    public void LAX_IND_IX_Works()
    {
        var test = new TestSpec
        {
            A = 0x00, X = 0x00,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAX_IND_IX,
            FinalValue     = 0x77,
            ExpectedA      = 0x77,
            ExpectedX      = 0x77,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.IND_IX);
    }

    [Fact]
    public void LAX_ZP_Y_Works()
    {
        var test = new TestSpec
        {
            A = 0x00, X = 0x00,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAX_ZP_Y,
            FinalValue     = 0x42,
            ExpectedA      = 0x42,
            ExpectedX      = 0x42,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ZP_Y);
    }
}
