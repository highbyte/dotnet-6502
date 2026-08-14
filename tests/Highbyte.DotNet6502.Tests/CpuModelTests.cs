namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Tests for the internal CPU model definition seam introduced by the CPU model
/// architecture work.
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
    public void Clone_Shares_The_Same_Immutable_Model_Definition()
    {
        var cpu = new CPU();
        var clone = cpu.Clone();

        Assert.Same(cpu.ModelDefinition, clone.ModelDefinition);
    }

    [Fact]
    public void Descriptor_Documented_Flag_Distinguishes_Official_From_Undocumented_OpCodes()
    {
        var cpu = new CPU(CpuCompatibilityProfile.FullUnofficial);

        Assert.True(cpu.Descriptors[(byte)OpCodeId.LDA_I]!.Documented);
        Assert.True(cpu.Descriptors[(byte)OpCodeId.JMP_IND]!.Documented);
        Assert.False(cpu.Descriptors[(byte)OpCodeId.LAX_ZP]!.Documented);
        Assert.False(cpu.Descriptors[(byte)OpCodeId.JAM_02]!.Documented);
    }

    [Fact]
    public void Nmos_Jmp_Indirect_Binds_Bespoke_Handler_With_Correct_Metadata()
    {
        var cpu = new CPU();
        var descriptor = cpu.Descriptors[(byte)OpCodeId.JMP_IND]!;

        // The NMOS model binds its own $6C handler (page-wrap bug); tooling metadata
        // matches the generic instruction shape.
        Assert.Equal(NmosHandlers.Jmp_Indirect, descriptor.Execute);
        Assert.Equal("JMP", descriptor.Mnemonic);
        Assert.Equal(AddrMode.Indirect, descriptor.Addressing);
        Assert.Equal(3, descriptor.Size);
        Assert.Equal(5ul, descriptor.BaseCycles);
    }

    [Fact]
    public void Descriptor_Execute_Runs_A_Complete_Instruction_And_Returns_Total_Cycles()
    {
        // LDA #$42 through the descriptor handler directly: operand fetch, register
        // update, flag update, and TOTAL cycle count in one call.
        var cpu = new CPU();
        var mem = new Memory();
        cpu.PC = 0x1000;
        mem[0x1000] = (byte)OpCodeId.LDA_I;
        mem[0x1001] = 0x42;

        var descriptor = cpu.Descriptors[(byte)OpCodeId.LDA_I]!;
        cpu.PC = 0x1001; // Executor fetches the opcode byte first; handler starts at the operand.
        var cycles = descriptor.Execute(cpu, mem);

        Assert.Equal(0x42, cpu.A);
        Assert.Equal((ushort)0x1002, cpu.PC);
        Assert.Equal(2ul, cycles);
    }
}
