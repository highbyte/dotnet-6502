using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Tests the public model-aware opcode metadata API (CPU.GetOpCodeInfo /
/// IsOpCodeDefined / GetOpCodeSize), including which opcodes each
/// compatibility profile defines on the NMOS models.
/// </summary>
public class OpCodeInfoTests
{
    [Fact]
    public void Default_CPU_Defines_OpCodes_With_Metadata()
    {
        var cpu = new CPU();

        var info = cpu.GetOpCodeInfo((byte)OpCodeId.NOP);

        Assert.NotNull(info);
        Assert.Equal((byte)OpCodeId.NOP, info.Value.Code);
        Assert.Equal("NOP", info.Value.Mnemonic);
        Assert.True(info.Value.Documented);
    }

    [Fact]
    public void OfficialOnly_Profile_Excludes_Unofficial_OpCodes()
    {
        var cpu = new CPU(CpuCompatibilityProfile.OfficialOnly);

        Assert.True(cpu.IsOpCodeDefined((byte)OpCodeId.NOP));
        Assert.False(cpu.IsOpCodeDefined((byte)OpCodeId.NOP_ILL_1A));
        Assert.False(cpu.IsOpCodeDefined((byte)OpCodeId.ARR_I));
    }

    [Fact]
    public void StableUnofficial_Profile_Includes_Stable_But_Not_Experimental_OpCodes()
    {
        var cpu = new CPU(CpuCompatibilityProfile.StableUnofficial);

        Assert.True(cpu.IsOpCodeDefined((byte)OpCodeId.NOP_ILL_1A));
        Assert.True(cpu.IsOpCodeDefined((byte)OpCodeId.SBC_I_EB));
        Assert.False(cpu.IsOpCodeDefined((byte)OpCodeId.ARR_I));
        Assert.False(cpu.IsOpCodeDefined((byte)OpCodeId.LAS_ABS_Y));
    }

    [Fact]
    public void ExperimentalUnofficial_Profile_Includes_Experimental_OpCodes()
    {
        var cpu = new CPU(CpuCompatibilityProfile.ExperimentalUnofficial);

        Assert.True(cpu.IsOpCodeDefined((byte)OpCodeId.ARR_I));
        Assert.True(cpu.IsOpCodeDefined((byte)OpCodeId.LAS_ABS_Y));
        Assert.False(cpu.IsOpCodeDefined((byte)OpCodeId.JAM_02));
    }

    [Fact]
    public void Default_CPU_Uses_ExperimentalUnofficial_Profile()
    {
        var defaultCpu = new CPU();
        var experimentalCpu = new CPU(CpuCompatibilityProfile.ExperimentalUnofficial);

        for (var code = 0; code <= 0xff; code++)
            Assert.Equal(experimentalCpu.IsOpCodeDefined((byte)code), defaultCpu.IsOpCodeDefined((byte)code));
        Assert.False(defaultCpu.IsOpCodeDefined((byte)OpCodeId.JAM_02));
    }

    [Fact]
    public void FullUnofficial_Profile_Includes_Halt_OpCodes()
    {
        var cpu = new CPU(CpuCompatibilityProfile.FullUnofficial);

        Assert.True(cpu.IsOpCodeDefined((byte)OpCodeId.ARR_I));
        Assert.True(cpu.IsOpCodeDefined((byte)OpCodeId.LAS_ABS_Y));
        Assert.True(cpu.IsOpCodeDefined((byte)OpCodeId.JAM_02));
    }

    [Theory]
    [InlineData(CpuCompatibilityProfile.OfficialOnly)]
    [InlineData(CpuCompatibilityProfile.StableUnofficial)]
    [InlineData(CpuCompatibilityProfile.ExperimentalUnofficial)]
    [InlineData(CpuCompatibilityProfile.FullUnofficial)]
    public void OpCodeInfo_Metadata_Is_Consistent_On_NMOS_Models(CpuCompatibilityProfile profile)
    {
        var cpu = new CPU(profile);

        for (var code = 0; code <= 0xff; code++)
        {
            var info = cpu.GetOpCodeInfo((byte)code);
            Assert.Equal(cpu.IsOpCodeDefined((byte)code), info is not null);
            if (info is null)
                continue;

            // On the NMOS models every defined opcode byte must map to a named
            // OpCodeId member, and the metadata views must agree.
            Assert.Equal((byte)code, info.Value.Code);
            Assert.True(((byte)code).IsDefinedAsOpCodeId(),
                $"OpCode byte {code:x2} has no corresponding OpCodeId enum member.");

            Assert.InRange(info.Value.Size, (byte)1, (byte)3);
            Assert.InRange(info.Value.MinimumCycles, 2ul, 8ul);
            Assert.Equal(info.Value.Size, cpu.GetOpCodeSize((byte)code));
            Assert.False(string.IsNullOrWhiteSpace(info.Value.Mnemonic));
        }
    }

    [Fact]
    public void Every_OpCodeId_Member_Is_Defined_On_A_FullUnofficial_NMOS_CPU()
    {
        // The OpCodeId enum enumerates the NMOS opcode bytes; the most permissive
        // profile must define every one of them.
        var cpu = new CPU(CpuCompatibilityProfile.FullUnofficial);

        foreach (var opCodeId in Enum.GetValues<OpCodeId>())
            Assert.True(cpu.IsOpCodeDefined((byte)opCodeId), $"{opCodeId} not defined.");
    }

    [Fact]
    public void GetOpCodeInfo_Is_Model_Aware()
    {
        // $9C: undefined on an OfficialOnly NMOS 6502, but STZ abs on the 65C02.
        var nmosCpu = new CPU(CpuCompatibilityProfile.OfficialOnly);
        var cmosCpu = new CPU(new NullLoggerFactory(), CpuModelIds.Ncr65c02, CpuCompatibilityProfile.OfficialOnly);

        Assert.Null(nmosCpu.GetOpCodeInfo(0x9C));

        var stz = cmosCpu.GetOpCodeInfo(0x9C);
        Assert.NotNull(stz);
        Assert.Equal("STZ", stz.Value.Mnemonic);
        Assert.Equal(AddrMode.ABS, stz.Value.AddressingMode);
        Assert.Equal(3, stz.Value.Size);
        Assert.True(stz.Value.Documented);
    }

    [Fact]
    public void GetOpCodeInfo_Reports_Undocumented_OpCodes_As_Not_Documented()
    {
        var cpu = new CPU(CpuCompatibilityProfile.FullUnofficial);

        Assert.True(cpu.GetOpCodeInfo((byte)OpCodeId.NOP)!.Value.Documented);
        Assert.False(cpu.GetOpCodeInfo((byte)OpCodeId.NOP_ILL_1A)!.Value.Documented);
        Assert.False(cpu.GetOpCodeInfo((byte)OpCodeId.JAM_02)!.Value.Documented);
    }
}
