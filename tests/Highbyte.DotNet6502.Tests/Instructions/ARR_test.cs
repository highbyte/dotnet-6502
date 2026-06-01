namespace Highbyte.DotNet6502.Tests.Instructions;

public class ARR_test
{
    [Fact]
    public void ARR_I_Takes_2_Cycles()
    {
        var test = new TestSpec
        {
            A = 0xFF, C = true,
            OpCode         = OpCodeId.ARR_I,
            FinalValue     = 0xFF,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ARR_I_ANDs_A_With_Immediate_Then_Rotates_Right()
    {
        // A=0xFF & imm=0xFF → 0xFF; old C=1 → bit7 of result; 0xFF>>1 | 0x80 = 0xFF
        var test = new TestSpec
        {
            A = 0xFF, C = true,
            OpCode     = OpCodeId.ARR_I,
            FinalValue = 0xFF,
            ExpectedA  = 0xFF,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ARR_I_Sets_Carry_From_Bit6_Of_Result()
    {
        // A=0xFF & imm=0xFF → 0xFF; C=1 → result=0xFF; C=bit6(0xFF)=1
        var test = new TestSpec
        {
            A = 0xFF, C = true,
            OpCode     = OpCodeId.ARR_I,
            FinalValue = 0xFF,
            ExpectedA  = 0xFF,
            ExpectedC  = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ARR_I_Clears_Carry_When_Bit6_Of_Result_Is_Clear()
    {
        // A=0x01 & imm=0x01 → 0x01; C=0 → ROR: result=0x00 (0x01>>1=0x00, no carry in); bit6(0x00)=0
        var test = new TestSpec
        {
            A = 0x01, C = false,
            OpCode     = OpCodeId.ARR_I,
            FinalValue = 0x01,
            ExpectedA  = 0x00,
            ExpectedC  = false,
            ExpectedZ  = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ARR_I_Sets_Overflow_As_Bit6_XOR_Bit5_Of_Result()
    {
        // A=0xFF & imm=0xFF → 0xFF; C=1 → result=0xFF; V=bit6^bit5=1^1=0
        var test = new TestSpec
        {
            A = 0xFF, C = true,
            OpCode     = OpCodeId.ARR_I,
            FinalValue = 0xFF,
            ExpectedV  = false, // bit6(1) XOR bit5(1) = 0
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ARR_I_Sets_Overflow_When_Bit6_And_Bit5_Differ()
    {
        // Need result where bit6=1, bit5=0 (or vice versa)
        // A=0xFF & imm=0x7F → 0x7F (bit7=0,bit6=1,bit5=1,bit4=1...)
        // C=1 → ROR(0x7F, C=1): result = (0x7F>>1) | 0x80 = 0x3F | 0x80 = 0xBF
        // bit6(0xBF)=0, bit5(0xBF)=1 → V=0^1=1
        var test = new TestSpec
        {
            A = 0xFF, C = true,
            OpCode     = OpCodeId.ARR_I,
            FinalValue = 0x7F,
            ExpectedA  = 0xBF,
            ExpectedV  = true,  // bit6(0)=0 XOR bit5(1)=1 → 1
            ExpectedC  = false, // bit6(0xBF)=0
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ARR_I_Sets_Negative_Flag_Based_On_Bit7_Of_Result()
    {
        // C=1 puts 1 in bit7 of the result
        var test = new TestSpec
        {
            A = 0xFF, C = true,
            OpCode     = OpCodeId.ARR_I,
            FinalValue = 0xFF,
            ExpectedN  = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }
}
