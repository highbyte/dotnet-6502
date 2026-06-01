namespace Highbyte.DotNet6502.Tests.Instructions;

public class NOP_Illegal_test
{
    [Fact]
    public void NOP_ILL_1A_Takes_2_Cycles()
    {
        var test = new TestSpec { OpCode = OpCodeId.NOP_ILL_1A, ExpectedCycles = 2 };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void NOP_ILL_DA_Takes_2_Cycles()
    {
        var test = new TestSpec { OpCode = OpCodeId.NOP_ILL_DA, ExpectedCycles = 2 };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void NOP_ILL_Implied_Has_No_Side_Effects()
    {
        var test = new TestSpec
        {
            A = 0x12, X = 0x34, Y = 0x56, SP = 0xff,
            C = false, Z = false, N = false, V = false,
            OpCode         = OpCodeId.NOP_ILL_3A,
            ExpectedA      = 0x12,
            ExpectedX      = 0x34,
            ExpectedY      = 0x56,
            ExpectedSP     = 0xff,
            ExpectedC      = false,
            ExpectedZ      = false,
            ExpectedN      = false,
            ExpectedV      = false,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void NOP_ILL_IMM_80_Takes_2_Cycles()
    {
        var test = new TestSpec
        {
            InsEffect      = InstrEffect.None,
            OpCode         = OpCodeId.NOP_ILL_IMM_80,
            FinalValue     = 0x42,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void NOP_ILL_ZP_04_Takes_3_Cycles()
    {
        var test = new TestSpec
        {
            InsEffect      = InstrEffect.None,
            OpCode         = OpCodeId.NOP_ILL_ZP_04,
            FinalValue     = 0x42,
            ExpectedCycles = 3,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void NOP_ILL_ZP_X_14_Takes_4_Cycles()
    {
        var test = new TestSpec
        {
            InsEffect      = InstrEffect.None,
            OpCode         = OpCodeId.NOP_ILL_ZP_X_14,
            FinalValue     = 0x42,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ZP_X);
    }

    [Fact]
    public void NOP_ILL_ABS_Takes_4_Cycles()
    {
        var test = new TestSpec
        {
            InsEffect      = InstrEffect.None,
            OpCode         = OpCodeId.NOP_ILL_ABS,
            FinalValue     = 0x42,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS);
    }

    [Fact]
    public void NOP_ILL_ABS_X_1C_Takes_4_Cycles()
    {
        var test = new TestSpec
        {
            InsEffect      = InstrEffect.None,
            OpCode         = OpCodeId.NOP_ILL_ABS_X_1C,
            FinalValue     = 0x42,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS_X);
    }

    [Fact]
    public void NOP_ILL_ABS_X_1C_Takes_5_Cycles_When_Page_Boundary_Crossed()
    {
        var test = new TestSpec
        {
            InsEffect      = InstrEffect.None,
            OpCode         = OpCodeId.NOP_ILL_ABS_X_1C,
            FinalValue     = 0x42,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ABS_X, fullAddress_Should_Cross_Page_Boundary: true);
    }
}
