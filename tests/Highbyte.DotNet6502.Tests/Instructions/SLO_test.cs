namespace Highbyte.DotNet6502.Tests.Instructions;

public class SLO_test
{
    [Fact]
    public void SLO_ZP_Takes_5_Cycles()
    {
        var test = new TestSpec
        {
            A = 0x01,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.SLO_ZP,
            FinalValue     = 0x40,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SLO_ZP_Shifts_Memory_Left_One_Bit()
    {
        var test = new TestSpec
        {
            A = 0x00,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.SLO_ZP,
            FinalValue     = 0x40,
            ExpectedMemVal = 0x80, // 0x40 << 1 = 0x80
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SLO_ZP_Sets_Carry_From_Old_Bit7_Of_Memory()
    {
        // mem = 0x80 (bit7=1) → after ASL: mem=0x00, C=1
        var test = new TestSpec
        {
            A = 0x00,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.SLO_ZP,
            FinalValue     = 0x80,
            ExpectedMemVal = 0x00,
            ExpectedC      = true,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SLO_ZP_ORs_Shifted_Value_Into_A()
    {
        // A=0x01, mem=0x40 → shifted=0x80, A = 0x01|0x80 = 0x81
        var test = new TestSpec
        {
            A = 0x01,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.SLO_ZP,
            FinalValue     = 0x40,
            ExpectedA      = 0x81,
            ExpectedN      = true,
            ExpectedZ      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SLO_ZP_Sets_Zero_Flag_When_A_ORA_Result_Is_Zero()
    {
        // A=0x00, mem=0x80 → shifted=0x00, A=0x00|0x00=0x00, Z=1
        var test = new TestSpec
        {
            A = 0x00,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.SLO_ZP,
            FinalValue     = 0x80,
            ExpectedA      = 0x00,
            ExpectedZ      = true,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SLO_ZP_X_Works() =>
        new TestSpec { A = 0x01, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.SLO_ZP_X,   FinalValue = 0x40, ExpectedMemVal = 0x80, ExpectedCycles = 6 }.Execute_And_Verify(AddrMode.ZP_X);

    [Fact]
    public void SLO_ABS_Works() =>
        new TestSpec { A = 0x01, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.SLO_ABS,    FinalValue = 0x40, ExpectedMemVal = 0x80, ExpectedCycles = 6 }.Execute_And_Verify(AddrMode.ABS);

    [Fact]
    public void SLO_ABS_X_Works() =>
        new TestSpec { A = 0x01, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.SLO_ABS_X,  FinalValue = 0x40, ExpectedMemVal = 0x80, ExpectedCycles = 7 }.Execute_And_Verify(AddrMode.ABS_X);

    [Fact]
    public void SLO_ABS_Y_Works() =>
        new TestSpec { A = 0x01, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.SLO_ABS_Y,  FinalValue = 0x40, ExpectedMemVal = 0x80, ExpectedCycles = 7 }.Execute_And_Verify(AddrMode.ABS_Y);

    [Fact]
    public void SLO_IX_IND_Works() =>
        new TestSpec { A = 0x01, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.SLO_IX_IND, FinalValue = 0x40, ExpectedMemVal = 0x80, ExpectedCycles = 8 }.Execute_And_Verify(AddrMode.IX_IND);

    [Fact]
    public void SLO_IND_IX_Works() =>
        new TestSpec { A = 0x01, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.SLO_IND_IX, FinalValue = 0x40, ExpectedMemVal = 0x80, ExpectedCycles = 8 }.Execute_And_Verify(AddrMode.IND_IX);
}
