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
        
        // Note: In DEBUG mode, GetAllInstructions() automatically verifies that the manual instruction list
        // matches the dynamically discovered instructions. If they don't match, it throws an exception.
        // This test validates that verification passes (by not throwing).
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
    public void VerifyInstructionListsMatch_Should_Throw_When_Counts_Differ()
    {
        // Arrange
        var list1 = new InstructionList(new List<Instruction> { new ADC(), new AND() });
        var list2 = new InstructionList(new List<Instruction> { new ADC() });

        // Act & Assert
        var ex = Assert.Throws<DotNet6502Exception>(() => 
            InstructionList.VerifyInstructionListsMatch(list1, list2));
        
        Assert.Contains("manual list has", ex.Message);
        Assert.Contains("dynamic discovery found", ex.Message);
    }
#endif
}
