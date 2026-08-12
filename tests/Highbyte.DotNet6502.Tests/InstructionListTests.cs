using Highbyte.DotNet6502.Instructions;

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
        Assert.NotNull(list.InstructionDictionary);
        Assert.True(list.OpCodeDictionary.Count > 0, "OpCodeDictionary should contain at least one opcode");
        Assert.True(list.InstructionDictionary.Count > 0, "InstructionDictionary should contain at least one instruction");
        
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

#if DEBUG
    [Fact]
    public void VerifyInstructionListsMatch_Should_Not_Throw_When_Counts_Match()
    {
        // Arrange
        var instructions = new List<Instruction> { new ADC(), new AND() };
        var list1 = new InstructionList(instructions);
        var list2 = new InstructionList(instructions);

        // Act & Assert - should not throw
        InstructionList.VerifyInstructionListsMatch(list1, list2);
    }

    [Fact]
    public void VerifyInstructionListsMatch_Should_Throw_When_An_OpCode_Is_Missing()
    {
        // Arrange
        var list1 = new InstructionList(new List<Instruction> { new ADC(), new AND() });
        var list2 = new InstructionList(new List<Instruction> { new ADC() });

        // Act & Assert
        var ex = Assert.Throws<DotNet6502Exception>(() =>
            InstructionList.VerifyInstructionListsMatch(list1, list2));

        Assert.Contains("Instruction list mismatch at opcode", ex.Message);
    }

    [Fact]
    public void VerifyInstructionListsMatch_Should_Throw_When_A_Byte_Maps_To_A_Different_Instruction()
    {
        // Arrange: same opcode byte count, but one byte remapped to another instruction
        // type. A count-only comparison would miss this.
        var adc = new ADC();
        var and = new AND();
        var opCodeByte = adc.OpCodes[0].CodeRaw;

        var correct = new InstructionList(
            new Dictionary<byte, OpCode> { [opCodeByte] = adc.OpCodes[0] },
            new Dictionary<byte, Instruction> { [opCodeByte] = adc });
        var remapped = new InstructionList(
            new Dictionary<byte, OpCode> { [opCodeByte] = adc.OpCodes[0] },
            new Dictionary<byte, Instruction> { [opCodeByte] = and });

        // Act & Assert
        var ex = Assert.Throws<DotNet6502Exception>(() =>
            InstructionList.VerifyInstructionListsMatch(correct, remapped));

        Assert.Contains("Instruction list mismatch at opcode", ex.Message);
    }
#endif
}
