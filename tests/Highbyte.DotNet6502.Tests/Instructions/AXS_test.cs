namespace Highbyte.DotNet6502.Tests.Instructions;

public class AXS_test
{
    [Fact]
    public void AXS_I_Takes_2_Cycles()
    {
        var test = new TestSpec
        {
            A = 0xFF, X = 0x0F,
            OpCode         = OpCodeId.AXS_I,
            FinalValue     = 0x05,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void AXS_I_Stores_A_AND_X_Minus_Immediate_In_X()
    {
        // A=0xFF, X=0x0F → A&X=0x0F; X = 0x0F - 0x05 = 0x0A
        var test = new TestSpec
        {
            A = 0xFF, X = 0x0F,
            OpCode     = OpCodeId.AXS_I,
            FinalValue = 0x05,
            ExpectedX  = 0x0A,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void AXS_I_Does_Not_Change_A()
    {
        var test = new TestSpec
        {
            A = 0xFF, X = 0x0F,
            OpCode     = OpCodeId.AXS_I,
            FinalValue = 0x05,
            ExpectedA  = 0xFF,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void AXS_I_Sets_Carry_When_A_AND_X_Greater_Or_Equal_To_Immediate()
    {
        // A&X=0x0F, imm=0x05 → 0x0F >= 0x05 → C=1
        var test = new TestSpec
        {
            A = 0xFF, X = 0x0F,
            OpCode     = OpCodeId.AXS_I,
            FinalValue = 0x05,
            ExpectedX  = 0x0A,
            ExpectedC  = true,
            ExpectedZ  = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void AXS_I_Clears_Carry_When_A_AND_X_Less_Than_Immediate()
    {
        // A=0x07, X=0x03 → A&X=0x03, imm=0x05 → 0x03 < 0x05 → C=0
        var test = new TestSpec
        {
            A = 0x07, X = 0x03,
            OpCode     = OpCodeId.AXS_I,
            FinalValue = 0x05,
            ExpectedC  = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void AXS_I_Sets_Zero_Flag_When_Result_Is_Zero()
    {
        // A=0x0F, X=0xFF → A&X=0x0F, imm=0x0F → X=0x00, Z=1, C=1 (equal)
        var test = new TestSpec
        {
            A = 0x0F, X = 0xFF,
            OpCode     = OpCodeId.AXS_I,
            FinalValue = 0x0F,
            ExpectedX  = 0x00,
            ExpectedZ  = true,
            ExpectedC  = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void AXS_I_Sets_Negative_Flag_When_Bit7_Of_Result_Set()
    {
        // A=0xFF, X=0xFF → A&X=0xFF, imm=0x80 → X=0x7F; (0xFF-0x80)=0x7F, N=0
        // Better: A=0xFF, X=0xFF → A&X=0xFF, imm=0x01 → X=0xFE, N=1
        var test = new TestSpec
        {
            A = 0xFF, X = 0xFF,
            OpCode     = OpCodeId.AXS_I,
            FinalValue = 0x01,
            ExpectedX  = 0xFE,
            ExpectedN  = true,
            ExpectedC  = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }
}
