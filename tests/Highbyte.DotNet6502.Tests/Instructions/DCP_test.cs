namespace Highbyte.DotNet6502.Tests.Instructions;

public class DCP_test
{
    [Fact]
    public void DCP_ZP_Takes_5_Cycles()
    {
        var test = new TestSpec
        {
            A = 0x05,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DCP_ZP,
            FinalValue     = 0x06,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void DCP_ZP_Decrements_Memory_Value()
    {
        var test = new TestSpec
        {
            A = 0x00,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DCP_ZP,
            FinalValue     = 0x06,
            ExpectedMemVal = 0x05,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void DCP_ZP_Sets_Zero_And_Carry_When_A_Equals_Decremented_Value()
    {
        // A = 5, mem = 6 → mem becomes 5, CMP(A=5, mem=5): Z=1, C=1 (A>=mem), N=0
        var test = new TestSpec
        {
            A = 0x05,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DCP_ZP,
            FinalValue     = 0x06,
            ExpectedMemVal = 0x05,
            ExpectedZ      = true,
            ExpectedC      = true,
            ExpectedN      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void DCP_ZP_Sets_Carry_And_Clears_Zero_When_A_Greater_Than_Decremented_Value()
    {
        // A = 10, mem = 6 → mem becomes 5, CMP(A=10, mem=5): Z=0, C=1, N=0
        var test = new TestSpec
        {
            A = 0x0A,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DCP_ZP,
            FinalValue     = 0x06,
            ExpectedMemVal = 0x05,
            ExpectedZ      = false,
            ExpectedC      = true,
            ExpectedN      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void DCP_ZP_Clears_Carry_When_A_Less_Than_Decremented_Value()
    {
        // A = 3, mem = 6 → mem becomes 5, CMP(A=3, mem=5): C=0 (A<mem)
        var test = new TestSpec
        {
            A = 0x03,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DCP_ZP,
            FinalValue     = 0x06,
            ExpectedMemVal = 0x05,
            ExpectedC      = false,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void DCP_ZP_Sets_Negative_Flag_When_Result_Has_Bit7_Set()
    {
        // A = 0x01, mem = 0x83 → mem becomes 0x82, CMP(1, 0x82): N = bit7(1-0x82) = bit7(0x7F) = 0...
        // Let's pick: A = 0x80, mem = 0x02 → mem = 0x01, CMP(0x80, 0x01): 0x80-0x01=0x7F, N=0, C=1
        // Better: A = 0x01, mem = 0x83 → mem=0x82, CMP(0x01,0x82): 0x01-0x82=0x7F... N=0
        // Even better: A = 0x00, mem = 0x01 → mem = 0x00, CMP(0x00, 0x00): Z=1, but that's Z
        // Actually: CMP sets N based on bit7 of (register - value)
        // A=0x10, mem=0x91 → mem=0x90, CMP(0x10, 0x90): 0x10-0x90=0x80, N=1
        var test = new TestSpec
        {
            A = 0x10,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DCP_ZP,
            FinalValue     = 0x91,
            ExpectedMemVal = 0x90,
            ExpectedN      = true,
            ExpectedC      = false, // 0x10 < 0x90
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void DCP_ZP_X_Works()
    {
        var test = new TestSpec
        {
            A = 0x05,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DCP_ZP_X,
            FinalValue     = 0x06,
            ExpectedMemVal = 0x05,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.ZP_X);
    }

    [Fact]
    public void DCP_ABS_Works()
    {
        var test = new TestSpec
        {
            A = 0x05,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DCP_ABS,
            FinalValue     = 0x06,
            ExpectedMemVal = 0x05,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.ABS);
    }

    [Fact]
    public void DCP_ABS_X_Works()
    {
        var test = new TestSpec
        {
            A = 0x05,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DCP_ABS_X,
            FinalValue     = 0x06,
            ExpectedMemVal = 0x05,
            ExpectedCycles = 7,
        };
        test.Execute_And_Verify(AddrMode.ABS_X);
    }

    [Fact]
    public void DCP_ABS_Y_Works()
    {
        var test = new TestSpec
        {
            A = 0x05,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DCP_ABS_Y,
            FinalValue     = 0x06,
            ExpectedMemVal = 0x05,
            ExpectedCycles = 7,
        };
        test.Execute_And_Verify(AddrMode.ABS_Y);
    }

    [Fact]
    public void DCP_IX_IND_Works()
    {
        var test = new TestSpec
        {
            A = 0x05,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DCP_IX_IND,
            FinalValue     = 0x06,
            ExpectedMemVal = 0x05,
            ExpectedCycles = 8,
        };
        test.Execute_And_Verify(AddrMode.IX_IND);
    }

    [Fact]
    public void DCP_IND_IX_Works()
    {
        var test = new TestSpec
        {
            A = 0x05,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.DCP_IND_IX,
            FinalValue     = 0x06,
            ExpectedMemVal = 0x05,
            ExpectedCycles = 8,
        };
        test.Execute_And_Verify(AddrMode.IND_IX);
    }
}
