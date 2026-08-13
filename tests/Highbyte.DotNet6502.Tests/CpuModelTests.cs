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

    [Theory]
    [InlineData(CpuCompatibilityProfile.OfficialOnly)]
    [InlineData(CpuCompatibilityProfile.StableUnofficial)]
    [InlineData(CpuCompatibilityProfile.ExperimentalUnofficial)]
    [InlineData(CpuCompatibilityProfile.FullUnofficial)]
    public void Descriptor_Table_Matches_InstructionList_Byte_For_Byte(CpuCompatibilityProfile profile)
    {
        var cpu = new CPU(profile);

        for (var code = 0; code <= 0xff; code++)
        {
            var b = (byte)code;
            var opCode = cpu.InstructionList.TryGetOpCode(b);
            var descriptor = cpu.Descriptors[b];

            if (opCode is null)
            {
                Assert.Null(descriptor);
                continue;
            }

            Assert.NotNull(descriptor);
            Assert.Equal(b, descriptor.Code);
            Assert.Equal(opCode.AddressingMode, descriptor.Addressing);
            Assert.Equal(opCode.Size, descriptor.Size);
            Assert.Equal(opCode.MinimumCycles, descriptor.BaseCycles);
            Assert.Equal(cpu.InstructionList.GetInstruction(opCode).Name, descriptor.Mnemonic);
        }
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
    public void Handler_Override_Keeps_Descriptor_Metadata_And_Replaces_Only_Execute()
    {
        var cpu = new CPU();
        var descriptor = cpu.Descriptors[(byte)OpCodeId.JMP_IND]!;

        // The NMOS model overrides $6C (page-wrap bug); tooling metadata is untouched.
        Assert.Equal(NmosHandlers.Jmp_Indirect, descriptor.Execute);
        Assert.Equal("JMP", descriptor.Mnemonic);
        Assert.Equal(AddrMode.Indirect, descriptor.Addressing);
        Assert.Equal(3, descriptor.Size);
        Assert.Equal(5ul, descriptor.BaseCycles);
    }

    [Fact]
    public void Handler_Override_For_An_Undefined_OpCode_Is_A_Construction_Error()
    {
        var instructionList = InstructionList.GetAllInstructions(CpuCompatibilityProfile.OfficialOnly);
        var overrides = new Dictionary<byte, ExecuteHandler>
        {
            [(byte)OpCodeId.LAX_ZP] = NmosHandlers.Jmp_Indirect, // LAX is undefined in OfficialOnly
        };

        Assert.Throws<DotNet6502Exception>(() => OpCodeDescriptorTableBuilder.Build(instructionList, overrides));
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
