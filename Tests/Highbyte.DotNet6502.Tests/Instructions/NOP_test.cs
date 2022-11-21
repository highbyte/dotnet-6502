using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions;

public class NOP_test
{

    [Fact]
    public void NOP_Takes_2_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.NOP,
            ExpectedCycles = 2,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }

    [Fact]
    public void NOP_Executes_With_No_Side_Effects_Other_Than_PC_Changed_To_Next_Instruction()
    {
        var test = new TestSpec()
        {
            A              = 0xa3,
            X              = 0x38,
            Y              = 0xf2,
            SP             = 0xff,
            C              = false,
            Z              = false,
            I              = false,
            D              = false,
            B              = false,
            U              = false,
            V              = false,
            N              = false,
            OpCode         = OpCodeId.NOP,
            ExpectedA      = 0xa3,
            ExpectedX      = 0x38,
            ExpectedY      = 0xf2,
            ExpectedSP     = 0xff,
            ExpectedC      = false,
            ExpectedZ      = false,
            ExpectedI      = false,
            ExpectedD      = false,
            ExpectedB      = false,
            ExpectedU      = false,
            ExpectedV      = false,
            ExpectedN      = false,
        };
        test.Execute_And_Verify(AddrMode.Implied);
    }
}
