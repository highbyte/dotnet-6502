namespace Highbyte.DotNet6502.Tests.Instructions;

public class LAS_test
{
    [Fact]
    public void LAS_ABS_Y_Takes_4_Cycles()
    {
        var test = new TestSpec
        {
            A = 0x00, X = 0x00, SP = 0x30,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAS_ABS_Y,
            FinalValue     = 0xFF,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS_Y);
    }

    [Fact]
    public void LAS_ABS_Y_Takes_5_Cycles_When_Page_Boundary_Crossed()
    {
        var test = new TestSpec
        {
            A = 0x00, X = 0x00, SP = 0x30,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAS_ABS_Y,
            FinalValue     = 0xFF,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ABS_Y, fullAddress_Should_Cross_Page_Boundary: true);
    }

    [Fact]
    public void LAS_ABS_Y_Stores_Memory_AND_SP_Into_A_X_And_SP()
    {
        // SP=0x30, mem=0xFF → result=0xFF&0x30=0x30; A=X=SP=0x30
        var test = new TestSpec
        {
            A = 0x00, X = 0x00, SP = 0x30,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAS_ABS_Y,
            FinalValue     = 0xFF,
            ExpectedA      = 0x30,
            ExpectedX      = 0x30,
            ExpectedSP     = 0x30,
            ExpectedN      = false,
            ExpectedZ      = false,
        };
        test.Execute_And_Verify(AddrMode.ABS_Y);
    }

    [Fact]
    public void LAS_ABS_Y_AND_Masks_Memory_With_SP()
    {
        // SP=0xF0, mem=0x0F → result=0xF0&0x0F=0x00; A=X=SP=0x00
        var test = new TestSpec
        {
            A = 0x01, X = 0x01, SP = 0xF0,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAS_ABS_Y,
            FinalValue     = 0x0F,
            ExpectedA      = 0x00,
            ExpectedX      = 0x00,
            ExpectedSP     = 0x00,
            ExpectedZ      = true,
            ExpectedN      = false,
        };
        test.Execute_And_Verify(AddrMode.ABS_Y);
    }

    [Fact]
    public void LAS_ABS_Y_Sets_Negative_Flag_When_Bit7_Set()
    {
        // SP=0xFF, mem=0x80 → result=0xFF&0x80=0x80; N=1
        var test = new TestSpec
        {
            A = 0x00, X = 0x00, SP = 0xFF,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.LAS_ABS_Y,
            FinalValue     = 0x80,
            ExpectedA      = 0x80,
            ExpectedX      = 0x80,
            ExpectedSP     = 0x80,
            ExpectedN      = true,
            ExpectedZ      = false,
        };
        test.Execute_And_Verify(AddrMode.ABS_Y);
    }
}
