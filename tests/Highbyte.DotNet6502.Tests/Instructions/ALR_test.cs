namespace Highbyte.DotNet6502.Tests.Instructions;

public class ALR_test
{
    [Fact]
    public void ALR_I_Takes_2_Cycles()
    {
        var test = new TestSpec
        {
            A = 0xFF,
            OpCode         = OpCodeId.ALR_I,
            FinalValue     = 0xAA,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ALR_I_ANDs_A_With_Immediate_Then_Shifts_Right()
    {
        // A=0xFF & imm=0xAA → 0xAA; LSR→ A=0x55, C=0 (bit0 of 0xAA=0)
        var test = new TestSpec
        {
            A = 0xFF,
            OpCode     = OpCodeId.ALR_I,
            FinalValue = 0xAA,
            ExpectedA  = 0x55,
            ExpectedC  = false,
            ExpectedN  = false,
            ExpectedZ  = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ALR_I_Sets_Carry_From_Bit0_Of_AND_Result()
    {
        // A=0xFF & imm=0x01 → 0x01; LSR→ A=0x00, C=1
        var test = new TestSpec
        {
            A = 0xFF,
            OpCode     = OpCodeId.ALR_I,
            FinalValue = 0x01,
            ExpectedA  = 0x00,
            ExpectedC  = true,
            ExpectedZ  = true,
            ExpectedN  = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ALR_I_Always_Clears_Negative_Flag()
    {
        // LSR always puts 0 in bit7, so N is always 0
        var test = new TestSpec
        {
            A = 0xFF,
            OpCode     = OpCodeId.ALR_I,
            FinalValue = 0xFF,
            ExpectedA  = 0x7F,
            ExpectedN  = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ALR_I_Sets_Zero_Flag_When_AND_Result_Is_Zero()
    {
        // A=0xF0 & imm=0x0F → 0x00; LSR→ A=0x00, Z=1
        var test = new TestSpec
        {
            A = 0xF0,
            OpCode     = OpCodeId.ALR_I,
            FinalValue = 0x0F,
            ExpectedA  = 0x00,
            ExpectedZ  = true,
            ExpectedC  = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }
}
