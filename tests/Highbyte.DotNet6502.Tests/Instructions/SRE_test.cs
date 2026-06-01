namespace Highbyte.DotNet6502.Tests.Instructions;

public class SRE_test
{
    [Fact]
    public void SRE_ZP_Takes_5_Cycles()
    {
        var test = new TestSpec
        {
            A = 0x00,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.SRE_ZP,
            FinalValue     = 0x02,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SRE_ZP_Shifts_Memory_Right_One_Bit()
    {
        var test = new TestSpec
        {
            A = 0x00,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.SRE_ZP,
            FinalValue     = 0x02,
            ExpectedMemVal = 0x01, // 0x02 >> 1 = 0x01
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SRE_ZP_Sets_Carry_From_Old_Bit0_Of_Memory()
    {
        // mem=0x01 (bit0=1) → shifted=0x00, C=1
        var test = new TestSpec
        {
            A = 0x00,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.SRE_ZP,
            FinalValue     = 0x01,
            ExpectedMemVal = 0x00,
            ExpectedC      = true,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SRE_ZP_EORs_Shifted_Value_Into_A()
    {
        // A=0x80, mem=0x02 → shifted=0x01, A = 0x80^0x01 = 0x81
        var test = new TestSpec
        {
            A = 0x80,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.SRE_ZP,
            FinalValue     = 0x02,
            ExpectedA      = 0x81,
            ExpectedN      = true,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SRE_ZP_Sets_Zero_Flag_When_Result_Is_Zero()
    {
        // A=0x01, mem=0x02 → shifted=0x01, A=0x01^0x01=0x00, Z=1
        var test = new TestSpec
        {
            A = 0x01,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.SRE_ZP,
            FinalValue     = 0x02,
            ExpectedA      = 0x00,
            ExpectedZ      = true,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void SRE_ZP_X_Works() =>
        new TestSpec { A = 0x00, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.SRE_ZP_X,   FinalValue = 0x02, ExpectedMemVal = 0x01, ExpectedCycles = 6 }.Execute_And_Verify(AddrMode.ZP_X);

    [Fact]
    public void SRE_ABS_Works() =>
        new TestSpec { A = 0x00, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.SRE_ABS,    FinalValue = 0x02, ExpectedMemVal = 0x01, ExpectedCycles = 6 }.Execute_And_Verify(AddrMode.ABS);

    [Fact]
    public void SRE_ABS_X_Works() =>
        new TestSpec { A = 0x00, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.SRE_ABS_X,  FinalValue = 0x02, ExpectedMemVal = 0x01, ExpectedCycles = 7 }.Execute_And_Verify(AddrMode.ABS_X);

    [Fact]
    public void SRE_ABS_Y_Works() =>
        new TestSpec { A = 0x00, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.SRE_ABS_Y,  FinalValue = 0x02, ExpectedMemVal = 0x01, ExpectedCycles = 7 }.Execute_And_Verify(AddrMode.ABS_Y);

    [Fact]
    public void SRE_IX_IND_Works() =>
        new TestSpec { A = 0x00, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.SRE_IX_IND, FinalValue = 0x02, ExpectedMemVal = 0x01, ExpectedCycles = 8 }.Execute_And_Verify(AddrMode.IX_IND);

    [Fact]
    public void SRE_IND_IX_Works() =>
        new TestSpec { A = 0x00, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.SRE_IND_IX, FinalValue = 0x02, ExpectedMemVal = 0x01, ExpectedCycles = 8 }.Execute_And_Verify(AddrMode.IND_IX);
}
