using Highbyte.DotNet6502;
using System.Reflection;

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
    }

#if DEBUG
    [Fact]
    public void GetAllInstructions_Verification_Throws_When_DynamicList_Mismatch()
    {
        // Arrange: create a fake InstructionList with different count to force verification failure
        var fakeOpCodes = new Dictionary<byte, OpCode>();
        var fakeInstructions = new Dictionary<byte, Instruction>();
        var fakeList = new InstructionList(fakeOpCodes, fakeInstructions);

        // Use reflection to set the private static override field
        var field = typeof(InstructionList).GetField("_getAllInstructionDynamicOverride", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);

        // Backup original
        var original = field.GetValue(null);

        try
        {
            // Set override to return the fake list
            Func<InstructionList> del = () => fakeList;
            field.SetValue(null, del);

            // Act & Assert: should throw due to mismatch
            Assert.Throws<Exception>(() => InstructionList.GetAllInstructions());
        }
        finally
        {
            // Restore original
            field.SetValue(null, original);
        }
    }
#endif
}
