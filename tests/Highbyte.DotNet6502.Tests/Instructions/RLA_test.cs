namespace Highbyte.DotNet6502.Tests.Instructions;

public class RLA_test
{
    [Fact]
    public void RLA_ZP_Takes_5_Cycles()
    {
        var test = new TestSpec
        {
            A = 0xFF, C = false,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.RLA_ZP,
            FinalValue     = 0x40,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void RLA_ZP_Rotates_Memory_Left_Through_Carry()
    {
        // C=0, mem=0x40 → ROL: bit7(0x40)=0→C, result=(0x40<<1)|0=0x80
        var test = new TestSpec
        {
            A = 0xFF, C = false,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.RLA_ZP,
            FinalValue     = 0x40,
            ExpectedMemVal = 0x80,
            ExpectedC      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void RLA_ZP_Sets_Carry_From_Old_Bit7_Of_Memory()
    {
        // C=0, mem=0x80 → ROL: bit7(0x80)=1→C=1, result=0x00|0=0x00
        var test = new TestSpec
        {
            A = 0xFF, C = false,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.RLA_ZP,
            FinalValue     = 0x80,
            ExpectedMemVal = 0x00,
            ExpectedC      = true,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void RLA_ZP_ANDs_Rotated_Value_Into_A()
    {
        // A=0xFF, C=0, mem=0x40 → ROL→0x80, A=0xFF&0x80=0x80, N=1
        var test = new TestSpec
        {
            A = 0xFF, C = false,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.RLA_ZP,
            FinalValue     = 0x40,
            ExpectedA      = 0x80,
            ExpectedN      = true,
            ExpectedZ      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void RLA_ZP_Sets_Zero_Flag_When_AND_Result_Is_Zero()
    {
        // A=0x0F, C=0, mem=0x40 → ROL→0x80, A=0x0F&0x80=0x00, Z=1
        var test = new TestSpec
        {
            A = 0x0F, C = false,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.RLA_ZP,
            FinalValue     = 0x40,
            ExpectedA      = 0x00,
            ExpectedZ      = true,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void RLA_ZP_X_Works() =>
        new TestSpec { A = 0xFF, C = false, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.RLA_ZP_X,   FinalValue = 0x40, ExpectedMemVal = 0x80, ExpectedCycles = 6 }.Execute_And_Verify(AddrMode.ZP_X);

    [Fact]
    public void RLA_ABS_Works() =>
        new TestSpec { A = 0xFF, C = false, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.RLA_ABS,    FinalValue = 0x40, ExpectedMemVal = 0x80, ExpectedCycles = 6 }.Execute_And_Verify(AddrMode.ABS);

    [Fact]
    public void RLA_ABS_X_Works() =>
        new TestSpec { A = 0xFF, C = false, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.RLA_ABS_X,  FinalValue = 0x40, ExpectedMemVal = 0x80, ExpectedCycles = 7 }.Execute_And_Verify(AddrMode.ABS_X);

    [Fact]
    public void RLA_ABS_Y_Works() =>
        new TestSpec { A = 0xFF, C = false, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.RLA_ABS_Y,  FinalValue = 0x40, ExpectedMemVal = 0x80, ExpectedCycles = 7 }.Execute_And_Verify(AddrMode.ABS_Y);

    [Fact]
    public void RLA_IX_IND_Works() =>
        new TestSpec { A = 0xFF, C = false, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.RLA_IX_IND, FinalValue = 0x40, ExpectedMemVal = 0x80, ExpectedCycles = 8 }.Execute_And_Verify(AddrMode.IX_IND);

    [Fact]
    public void RLA_IND_IX_Works() =>
        new TestSpec { A = 0xFF, C = false, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.RLA_IND_IX, FinalValue = 0x40, ExpectedMemVal = 0x80, ExpectedCycles = 8 }.Execute_And_Verify(AddrMode.IND_IX);
}
