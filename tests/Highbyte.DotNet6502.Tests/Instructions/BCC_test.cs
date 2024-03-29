namespace Highbyte.DotNet6502.Tests.Instructions;

public class BCC_test
{

    [Fact]
    public void BCC_I_Takes_2_Cycles_If_Branch_Fails()
    {
        var test = new TestSpec()
        {
            PC             = 0x2000,
            C              = true, // BCC does not branch if Carry flag is set.
            OpCode         = OpCodeId.BCC,
            FinalValue     = 0x20,
            ExpectedCycles = 2
        };
        test.Execute_And_Verify(AddrMode.Relative);
    }

    [Fact]
    public void BCC_I_Takes_3_Cycles_Within_Same_Page()
    {
        var test = new TestSpec()
        {
            PC             = 0x2000,
            C              = false, // BCC branches if Carry flag is clear.
            OpCode         = OpCodeId.BCC,
            FinalValue     = 0x20,
            ExpectedCycles = 3
        };
        test.Execute_And_Verify(AddrMode.Relative);
    }

    [Fact]
    public void BCC_I_Takes_4_Cycles_If_Branch_Succeeds_And_Page_Boundary_Is_Crossed()
    {
        var test = new TestSpec()
        {
            PC             = 0x20f0,
            C              = false, // BCC branches if Carry flag is clear.
            OpCode         = OpCodeId.BCC,
            FinalValue     = 0x20,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.Relative);
    }        

    [Fact]
    public void BCC_I_Does_Not_Jump_To_New_Location_If_Branch_Fails()
    {
        var test = new TestSpec()
        {
            PC             = 0x2000,
            C              = true, // BCC does not branch if Carry flag is set.
            OpCode         = OpCodeId.BCC,
            FinalValue     = 0x20,
            ExpectedPC     = 0x2000 + 0x02 // When branhing fails, the PC should remain unchanged and point to next instruction after this one
        };
        test.Execute_And_Verify(AddrMode.Relative);
    }

    [Fact]
    public void BCC_I_Jumps_To_Correct_Location_When_Offset_Is_Positive()
    {
        var test = new TestSpec()
        {
            PC             = 0x2000,
            C              = false, // BCC branches if Carry flag is clear.
            OpCode         = OpCodeId.BCC,
            FinalValue     = 0x20,
            ExpectedPC     = 0x2000 + 0x02 + 0x20,  // The relative branch location should be where the PC is after the branching instruction has updated the PC (reading instruction + operand)
        };
        test.Execute_And_Verify(AddrMode.Relative);
    }

    [Fact]
    public void BCC_I_Jumps_To_Correct_Location_When_Offset_Is_Negative()
    {
        var test = new TestSpec()
        {
            PC             = 0x2010,
            C              = false, // BCC branches if Carry flag is clear.
            OpCode         = OpCodeId.BCC,
            FinalValue     = 0xf0, // - 10
            ExpectedPC     = 0x2010 + 0x02 - 0x10,
        };
        test.Execute_And_Verify(AddrMode.Relative);
    }        

    [Fact]
    public void BCC_I_Jumps_To_Correct_Location_When_Offset_Crosses_Page_Boundary()
    {
        var test = new TestSpec()
        {
            PC             = 0x20f0,
            C              = false, // BCC branches if Carry flag is clear.
            OpCode         = OpCodeId.BCC,
            FinalValue     = 0x20, // + 20
            ExpectedPC     = 0x20f0 + 0x02 + 0x20,
        };
        test.Execute_And_Verify(AddrMode.Relative);
    }        
}
