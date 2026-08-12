namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Tests for the internal CPU model definition seam introduced by the CPU model
/// architecture work (feature cpu-models-65c02, M1 step 1).
/// </summary>
public class CpuModelTests
{
    [Fact]
    public void CPU_Is_Constructed_With_The_Nmos6502_Model()
    {
        var cpu = new CPU();

        Assert.Equal(CpuModelIds.Nmos6502, cpu.ModelDefinition.ModelId);
    }

    [Theory]
    [InlineData(CpuCompatibilityProfile.OfficialOnly)]
    [InlineData(CpuCompatibilityProfile.StableUnofficial)]
    [InlineData(CpuCompatibilityProfile.ExperimentalUnofficial)]
    [InlineData(CpuCompatibilityProfile.FullUnofficial)]
    public void Nmos6502_Model_Supports_All_Compatibility_Profiles(CpuCompatibilityProfile profile)
    {
        var cpu = new CPU(profile);

        Assert.Equal(profile, cpu.CompatibilityProfile);
        Assert.Contains(profile, cpu.ModelDefinition.SupportedProfiles);
    }

    [Fact]
    public void Unknown_Model_Id_Is_Rejected()
    {
        Assert.Throws<DotNet6502Exception>(() => CpuModels.GetDefinition("no-such-model"));
    }

    [Fact]
    public void Model_InstructionList_Matches_Direct_GetAllInstructions()
    {
        // The definition must be a pure indirection: the table it builds is identical
        // (per opcode byte) to what InstructionList.GetAllInstructions produced before.
        var fromModel = Nmos6502Model.Definition.CreateInstructionList(CpuCompatibilityProfile.ExperimentalUnofficial);
        var direct = InstructionList.GetAllInstructions(CpuCompatibilityProfile.ExperimentalUnofficial);

        for (var code = 0; code <= 0xff; code++)
        {
            var b = (byte)code;
            var modelOpCode = fromModel.TryGetOpCode(b);
            var directOpCode = direct.TryGetOpCode(b);
            Assert.Equal(directOpCode is null, modelOpCode is null);
            if (modelOpCode is null || directOpCode is null)
                continue;
            Assert.Equal(directOpCode.AddressingMode, modelOpCode.AddressingMode);
            Assert.Equal(directOpCode.Size, modelOpCode.Size);
            Assert.Equal(directOpCode.MinimumCycles, modelOpCode.MinimumCycles);
            Assert.Equal(direct.GetInstruction(directOpCode).GetType(), fromModel.GetInstruction(modelOpCode).GetType());
        }
    }

    [Fact]
    public void Clone_Shares_The_Same_Immutable_Model_Definition()
    {
        var cpu = new CPU();
        var clone = cpu.Clone();

        Assert.Same(cpu.ModelDefinition, clone.ModelDefinition);
    }
}
