namespace Highbyte.DotNet6502.Tests.Instructions;

public class ANC_test
{
    [Fact]
    public void ANC_0B_Takes_2_Cycles()
    {
        var test = new TestSpec
        {
            A = 0xFF,
            OpCode         = OpCodeId.ANC_I_0B,
            FinalValue     = 0xF0,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ANC_0B_ANDs_A_With_Immediate()
    {
        var test = new TestSpec
        {
            A = 0xFF,
            OpCode     = OpCodeId.ANC_I_0B,
            FinalValue = 0xF0,
            ExpectedA  = 0xF0,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ANC_0B_Sets_Carry_From_Bit7_Of_Result()
    {
        // A=0xFF & imm=0xF0 → result=0xF0, bit7=1 → C=1
        var test = new TestSpec
        {
            A = 0xFF,
            OpCode     = OpCodeId.ANC_I_0B,
            FinalValue = 0xF0,
            ExpectedA  = 0xF0,
            ExpectedC  = true,
            ExpectedN  = true,
            ExpectedZ  = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ANC_0B_Clears_Carry_When_Bit7_Of_Result_Is_Clear()
    {
        // A=0xFF & imm=0x0F → result=0x0F, bit7=0 → C=0
        var test = new TestSpec
        {
            A = 0xFF,
            OpCode     = OpCodeId.ANC_I_0B,
            FinalValue = 0x0F,
            ExpectedA  = 0x0F,
            ExpectedC  = false,
            ExpectedN  = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ANC_0B_Sets_Zero_Flag_When_Result_Is_Zero()
    {
        var test = new TestSpec
        {
            A = 0xF0,
            OpCode     = OpCodeId.ANC_I_0B,
            FinalValue = 0x0F,
            ExpectedA  = 0x00,
            ExpectedZ  = true,
            ExpectedC  = false,
        };
        test.Execute_And_Verify(AddrMode.I);
    }

    [Fact]
    public void ANC_2B_Behaves_Identically_To_0B()
    {
        // Both opcodes must produce the same result
        var test = new TestSpec
        {
            A = 0xFF,
            OpCode     = OpCodeId.ANC_I_2B,
            FinalValue = 0xF0,
            ExpectedA  = 0xF0,
            ExpectedC  = true,
            ExpectedN  = true,
        };
        test.Execute_And_Verify(AddrMode.I);
    }
}
