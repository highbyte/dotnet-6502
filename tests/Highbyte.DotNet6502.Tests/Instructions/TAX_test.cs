namespace Highbyte.DotNet6502.Tests.Instructions;

public class TAX_test
{
    [Fact]
    public void TAX_Takes_2_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.TAX,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void TAX_Transfers_A_To_X()
    {
        var test = new TestSpec()
        {
            A              = 0x34,
            X              = 0x22,
            OpCode         = OpCodeId.TAX,
            ExpectedX      = 0x34,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void TAX_ZP_Sets_Zero_Flag_If_Result_Is_Zero()
    {
        var test = new TestSpec()
        {
            A              = 0x00,
            X              = 0x01,
            OpCode         = OpCodeId.TAX,
            ExpectedZ      = true,
            ExpectedX      = 0x00,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void TAX_ZP_Clears_Zero_Flag_If_Result_Is_Not_Zero()
    {
        var test = new TestSpec()
        {
            A              = 0x34,
            X              = 0x01,
            OpCode         = OpCodeId.TAX,
            ExpectedZ      = false,
            ExpectedX      = 0x34,
        };
        test.Execute_And_Verify(AddrMode.Implied);        
    }

    [Fact]
    public void TAX_ZP_Sets_Negative_Flag_If_Result_Is_Negative()
    {
        var test = new TestSpec()
        {
            A              = 0xfe,
            X              = 0x00,
            OpCode         = OpCodeId.TAX,
            ExpectedN      = true,
            ExpectedX      = 0xfe,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void TAX_ZP_Clears_Negative_Flag_If_Result_Is_Positive()
    {
        var test = new TestSpec()
        {
            A              = 0x34,
            X              = 0x01,
            OpCode         = OpCodeId.TAX,
            ExpectedN      = false,
            ExpectedX      = 0x34,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }
}
