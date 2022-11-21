using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions;

public class TYA_test
{
    [Fact]
    public void TYA_Takes_2_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.TYA,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void TYA_Transfers_Y_To_A()
    {
        var test = new TestSpec()
        {
            A              = 0x34,
            Y              = 0x22,
            OpCode         = OpCodeId.TYA,
            ExpectedA      = 0x22,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void TYA_Sets_Zero_Flag_If_Result_Is_Zero()
    {
        var test = new TestSpec()
        {
            A              = 0x34,
            Y              = 0x00,
            OpCode         = OpCodeId.TYA,
            ExpectedZ      = true,
            ExpectedA      = 0x00,
        };
        test.Execute_And_Verify(AddrMode.Implied);        }

    [Fact]
    public void TYA_Clears_Zero_Flag_If_Result_Is_Not_Zero()
    {
        var test = new TestSpec()
        {
            A              = 0x34,
            Y              = 0x01,
            OpCode         = OpCodeId.TYA,
            ExpectedZ      = false,
            ExpectedA      = 0x01,
        };
        test.Execute_And_Verify(AddrMode.Implied);        
    }

    [Fact]
    public void TYA_Sets_Negative_Flag_If_Result_Is_Negative()
    {
        var test = new TestSpec()
        {
            A              = 0x34,
            Y              = 0xfe,
            OpCode         = OpCodeId.TYA,
            ExpectedN      = true,
            ExpectedA      = 0xfe,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void TYA_Clears_Negative_Flag_If_Result_Is_Positive()
    {
        var test = new TestSpec()
        {
            A              = 0x34,
            Y              = 0x01,
            OpCode         = OpCodeId.TYA,
            ExpectedN      = false,
            ExpectedA      = 0x01,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }
}
