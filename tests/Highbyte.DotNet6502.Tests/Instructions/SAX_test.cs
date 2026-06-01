namespace Highbyte.DotNet6502.Tests.Instructions;

public class SAX_test
{
    [Fact]
    public void SAX_ZP_Takes_3_Cycles()
    {
        var test = new TestSpec
        {
            A = 0xFF, X = 0x0F,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.SAX_ZP,
            FinalValue     = 0x00,
            ExpectedCycles = 3,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SAX_ZP_Stores_A_AND_X_To_Memory()
    {
        var test = new TestSpec
        {
            A = 0xFF, X = 0x0F,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.SAX_ZP,
            FinalValue     = 0x00,
            ExpectedMemVal = 0x0F, // 0xFF & 0x0F = 0x0F
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SAX_ZP_Stores_Zero_When_A_And_X_Have_No_Common_Bits()
    {
        var test = new TestSpec
        {
            A = 0xF0, X = 0x0F,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.SAX_ZP,
            FinalValue     = 0xFF,
            ExpectedMemVal = 0x00, // 0xF0 & 0x0F = 0x00
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SAX_ZP_Does_Not_Change_A_Or_X()
    {
        var test = new TestSpec
        {
            A = 0xAB, X = 0xCD,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.SAX_ZP,
            FinalValue     = 0x00,
            ExpectedA      = 0xAB,
            ExpectedX      = 0xCD,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SAX_ABS_Works()
    {
        var test = new TestSpec
        {
            A = 0xFF, X = 0xF0,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.SAX_ABS,
            FinalValue     = 0x00,
            ExpectedMemVal = 0xF0,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS);
    }

    [Fact]
    public void SAX_IX_IND_Works()
    {
        var test = new TestSpec
        {
            A = 0x3C, X = 0x0F,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.SAX_IX_IND,
            FinalValue     = 0x00,
            ExpectedMemVal = 0x0C, // 0x3C & 0x0F = 0x0C
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.IX_IND);
    }

    [Fact]
    public void SAX_ZP_Y_Works()
    {
        var test = new TestSpec
        {
            A = 0xFF, X = 0xAA,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.SAX_ZP_Y,
            FinalValue     = 0x00,
            ExpectedMemVal = 0xAA, // 0xFF & 0xAA = 0xAA
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ZP_Y);
    }
}
