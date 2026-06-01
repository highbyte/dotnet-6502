namespace Highbyte.DotNet6502.Tests.Instructions;

public class RRA_test
{
    [Fact]
    public void RRA_ZP_Takes_5_Cycles()
    {
        var test = new TestSpec
        {
            A = 0x10, C = false,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.RRA_ZP,
            FinalValue     = 0x04,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void RRA_ZP_Rotates_Memory_Right_Through_Carry()
    {
        // C=0, mem=0x04 → ROR: bit0(0x04)=0→C, result=(0x04>>1)|0x00=0x02
        var test = new TestSpec
        {
            A = 0x00, C = false,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.RRA_ZP,
            FinalValue     = 0x04,
            ExpectedMemVal = 0x02,
            ExpectedC      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void RRA_ZP_Rotated_Out_Bit_Becomes_Carry_Input_For_ADC()
    {
        // C=0, mem=0x01 → ROR: C=1 (old bit0), result=0x00; ADC(A=0x00, 0x00, C=1): A=0x01
        var test = new TestSpec
        {
            A = 0x00, C = false,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.RRA_ZP,
            FinalValue     = 0x01,
            ExpectedA      = 0x01, // ADC(0, 0, C=1) = 1
            ExpectedC      = false,
            ExpectedZ      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void RRA_ZP_ADCs_Rotated_Value_Into_A()
    {
        // A=0x10, C=0, mem=0x04 → ROR→0x02, C=0; ADC(0x10, 0x02, C=0)=0x12
        var test = new TestSpec
        {
            A = 0x10, C = false,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.RRA_ZP,
            FinalValue     = 0x04,
            ExpectedA      = 0x12,
            ExpectedZ      = false,
            ExpectedN      = false,
            ExpectedC      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void RRA_ZP_Sets_Carry_When_ADC_Overflows()
    {
        // A=0xFF, C=0, mem=0x02 → ROR→0x01, C=0; ADC(0xFF, 0x01, C=0)=0x00, C=1
        var test = new TestSpec
        {
            A = 0xFF, C = false,
            InsEffect      = InstrEffect.Reg,
            OpCode         = OpCodeId.RRA_ZP,
            FinalValue     = 0x02,
            ExpectedA      = 0x00,
            ExpectedC      = true,
            ExpectedZ      = true,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void RRA_ZP_X_Works() =>
        new TestSpec { A = 0x00, C = false, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.RRA_ZP_X,   FinalValue = 0x04, ExpectedMemVal = 0x02, ExpectedCycles = 6 }.Execute_And_Verify(AddrMode.ZP_X);

    [Fact]
    public void RRA_ABS_Works() =>
        new TestSpec { A = 0x00, C = false, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.RRA_ABS,    FinalValue = 0x04, ExpectedMemVal = 0x02, ExpectedCycles = 6 }.Execute_And_Verify(AddrMode.ABS);

    [Fact]
    public void RRA_ABS_X_Works() =>
        new TestSpec { A = 0x00, C = false, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.RRA_ABS_X,  FinalValue = 0x04, ExpectedMemVal = 0x02, ExpectedCycles = 7 }.Execute_And_Verify(AddrMode.ABS_X);

    [Fact]
    public void RRA_ABS_Y_Works() =>
        new TestSpec { A = 0x00, C = false, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.RRA_ABS_Y,  FinalValue = 0x04, ExpectedMemVal = 0x02, ExpectedCycles = 7 }.Execute_And_Verify(AddrMode.ABS_Y);

    [Fact]
    public void RRA_IX_IND_Works() =>
        new TestSpec { A = 0x00, C = false, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.RRA_IX_IND, FinalValue = 0x04, ExpectedMemVal = 0x02, ExpectedCycles = 8 }.Execute_And_Verify(AddrMode.IX_IND);

    [Fact]
    public void RRA_IND_IX_Works() =>
        new TestSpec { A = 0x00, C = false, InsEffect = InstrEffect.Mem, OpCode = OpCodeId.RRA_IND_IX, FinalValue = 0x04, ExpectedMemVal = 0x02, ExpectedCycles = 8 }.Execute_And_Verify(AddrMode.IND_IX);
}
