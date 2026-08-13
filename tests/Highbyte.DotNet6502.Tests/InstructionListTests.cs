namespace Highbyte.DotNet6502.Tests;

public class InstructionListTests
{
    [Fact]
    public void GetAllInstructions_Should_NotThrow_And_Return_List()
    {
        // Act
        var list = InstructionList.GetAllInstructions();

        // Assert
        Assert.NotNull(list);
        Assert.NotNull(list.OpCodeDictionary);
        Assert.True(list.OpCodeDictionary.Count > 0, "OpCodeDictionary should contain at least one opcode");

        // This test ensures the default instruction list can be created successfully.
    }

    [Fact]
    public void GetAllInstructions_With_OfficialOnly_Profile_Excludes_Unofficial_OpCodes()
    {
        var list = InstructionList.GetAllInstructions(CpuCompatibilityProfile.OfficialOnly);

        Assert.Contains((byte)OpCodeId.NOP, list.OpCodeDictionary.Keys);
        Assert.DoesNotContain((byte)OpCodeId.NOP_ILL_1A, list.OpCodeDictionary.Keys);
        Assert.DoesNotContain((byte)OpCodeId.ARR_I, list.OpCodeDictionary.Keys);
    }

    [Fact]
    public void GetAllInstructions_With_StableUnofficial_Profile_Includes_Stable_But_Not_Experimental_OpCodes()
    {
        var list = InstructionList.GetAllInstructions(CpuCompatibilityProfile.StableUnofficial);

        Assert.Contains((byte)OpCodeId.NOP_ILL_1A, list.OpCodeDictionary.Keys);
        Assert.Contains((byte)OpCodeId.SBC_I_EB, list.OpCodeDictionary.Keys);
        Assert.DoesNotContain((byte)OpCodeId.ARR_I, list.OpCodeDictionary.Keys);
        Assert.DoesNotContain((byte)OpCodeId.LAS_ABS_Y, list.OpCodeDictionary.Keys);
    }

    [Fact]
    public void GetAllInstructions_With_ExperimentalUnofficial_Profile_Includes_Experimental_OpCodes()
    {
        var list = InstructionList.GetAllInstructions(CpuCompatibilityProfile.ExperimentalUnofficial);

        Assert.Contains((byte)OpCodeId.ARR_I, list.OpCodeDictionary.Keys);
        Assert.Contains((byte)OpCodeId.LAS_ABS_Y, list.OpCodeDictionary.Keys);
        Assert.DoesNotContain((byte)OpCodeId.JAM_02, list.OpCodeDictionary.Keys);
    }

    [Fact]
    public void GetAllInstructions_Without_Profile_Uses_ExperimentalUnofficial_Default()
    {
        var defaultList = InstructionList.GetAllInstructions();
        var experimentalList = InstructionList.GetAllInstructions(CpuCompatibilityProfile.ExperimentalUnofficial);

        Assert.Equal(experimentalList.OpCodeDictionary.Keys, defaultList.OpCodeDictionary.Keys);
        Assert.DoesNotContain((byte)OpCodeId.JAM_02, defaultList.OpCodeDictionary.Keys);
    }

    [Fact]
    public void GetAllInstructions_With_FullUnofficial_Profile_Includes_Halt_OpCodes()
    {
        var fullList = InstructionList.GetAllInstructions(CpuCompatibilityProfile.FullUnofficial);

        Assert.Contains((byte)OpCodeId.ARR_I, fullList.OpCodeDictionary.Keys);
        Assert.Contains((byte)OpCodeId.LAS_ABS_Y, fullList.OpCodeDictionary.Keys);
        Assert.Contains((byte)OpCodeId.JAM_02, fullList.OpCodeDictionary.Keys);
    }

    [Theory]
    [InlineData(CpuCompatibilityProfile.OfficialOnly)]
    [InlineData(CpuCompatibilityProfile.StableUnofficial)]
    [InlineData(CpuCompatibilityProfile.ExperimentalUnofficial)]
    [InlineData(CpuCompatibilityProfile.FullUnofficial)]
    public void GetAllInstructions_OpCode_Metadata_Is_Consistent(CpuCompatibilityProfile profile)
    {
        var list = InstructionList.GetAllInstructions(profile);

        foreach (var (code, opCode) in list.OpCodeDictionary)
        {
            // The enum representation and the raw byte must agree, and every opcode
            // byte must map to a named OpCodeId member.
            Assert.Equal(code, opCode.CodeRaw);
            Assert.True(Enum.IsDefined(opCode.Code),
                $"OpCode byte {code:x2} has no corresponding OpCodeId enum member.");

            Assert.InRange(opCode.Size, 1, 3);
            Assert.InRange(opCode.MinimumCycles, 2ul, 8ul);
            Assert.Same(opCode, list.TryGetOpCode(code));
        }
    }

    [Fact]
    public void Every_OpCodeId_Member_Is_Present_In_The_FullUnofficial_List()
    {
        // The OpCodeId enum enumerates the NMOS opcode bytes; the most permissive
        // profile must define every one of them (and nothing that isn't named).
        var list = InstructionList.GetAllInstructions(CpuCompatibilityProfile.FullUnofficial);

        foreach (var opCodeId in Enum.GetValues<OpCodeId>())
            Assert.Contains((byte)opCodeId, list.OpCodeDictionary.Keys);
    }
}
