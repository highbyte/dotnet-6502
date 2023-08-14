namespace Highbyte.DotNet6502.Tests.Instructions;

public class DEX_test
{
    [Fact]
    public void DEX_Takes_2_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.DEX,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void DEX_Decreases_Register()
    {
        var test = new TestSpec()
        {
            X              = 0x02,
            OpCode         = OpCodeId.DEX,
            ExpectedX      = 0x01,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void DEX_Sets_Zero_Flag_If_Result_Is_Zero()
    {
        var test = new TestSpec()
        {
            X              = 0x01,
            OpCode         = OpCodeId.DEX,
            ExpectedX      = 0x00,
            ExpectedZ      = true,
        };
        test.Execute_And_Verify(AddrMode.Implied);        }

    [Fact]
    public void DEX_Clears_Zero_Flag_If_Result_Is_Not_Zero()
    {
        var test = new TestSpec()
        {
            X              = 0x02,
            OpCode         = OpCodeId.DEX,
            ExpectedX      = 0x01,
            ExpectedZ      = false,
        };
        test.Execute_And_Verify(AddrMode.Implied);        
    }

    [Fact]
    public void DEX_Sets_Negative_Flag_If_Result_Is_Negative()
    {
        var test = new TestSpec()
        {
            X              = 0x00,
            OpCode         = OpCodeId.DEX,
            ExpectedX      = 0xff,
            ExpectedN      = true,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void DEX_Clears_Negative_Flag_If_Result_Is_Positive()
    {
        var test = new TestSpec()
        {
            X              = 0x02,
            OpCode         = OpCodeId.DEX,
            ExpectedX      = 0x01,
            ExpectedN      = false,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }
}
