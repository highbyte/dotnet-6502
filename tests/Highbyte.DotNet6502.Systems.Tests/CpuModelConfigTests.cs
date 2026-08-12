using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems.Apple2;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests;

/// <summary>
/// CPU model selection through system configuration (feature cpu-models-65c02, M1
/// step 6): the Apple II and the Generic computer can be configured with the 65C02;
/// defaults stay NMOS. $9C (STZ abs, 3 bytes on a 65C02; undefined on NMOS profiles)
/// is used as the observable model probe.
/// </summary>
public class CpuModelConfigTests
{
    private const byte StzAbsOpCode = 0x9C;

    // ---- Apple II ----

    [Fact]
    public void Apple2_Defaults_To_Nmos6502()
    {
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);

        Assert.False(apple2.CPU.IsOpCodeDefined(StzAbsOpCode));
    }

    [Fact]
    public void Apple2_Can_Be_Configured_With_The_Ncr65c02()
    {
        var config = new Apple2Config
        {
            CpuModelId = CpuModelIds.Ncr65c02,
            CpuCompatibilityProfile = CpuCompatibilityProfile.OfficialOnly,
        };
        var apple2 = new Apple2System(config, NullLoggerFactory.Instance);

        Assert.True(apple2.CPU.IsOpCodeDefined(StzAbsOpCode));
        Assert.Equal(3, apple2.CPU.GetOpCodeSize(StzAbsOpCode));
    }

    [Fact]
    public void Apple2SystemConfig_Rejects_65c02_With_Unofficial_Profile()
    {
        var systemConfig = new Apple2SystemConfig
        {
            CpuModelId = CpuModelIds.Ncr65c02,
            CpuCompatibilityProfile = CpuCompatibilityProfile.StableUnofficial,
        };

        Assert.False(systemConfig.IsValid(out var errors));
        Assert.Contains(errors, e => e.Contains("does not support compatibility profile"));
    }

    [Fact]
    public void Apple2SystemConfig_Rejects_Unknown_Model_Id()
    {
        var systemConfig = new Apple2SystemConfig
        {
            CpuModelId = "z80", // very much not a 6502
        };

        Assert.False(systemConfig.IsValid(out var errors));
        Assert.Contains(errors, e => e.Contains("Unknown CPU model id"));
    }

    // ---- Generic computer ----

    [Fact]
    public void GenericComputer_Defaults_To_Nmos6502()
    {
        var genericComputer = new GenericComputer();

        Assert.False(genericComputer.CPU.IsOpCodeDefined(StzAbsOpCode));
    }

    [Fact]
    public void GenericComputer_Can_Be_Configured_With_The_Ncr65c02()
    {
        var config = new GenericComputerConfig
        {
            CpuModelId = CpuModelIds.Ncr65c02,
            CpuCompatibilityProfile = CpuCompatibilityProfile.OfficialOnly,
        };
        var genericComputer = new GenericComputer(config, NullLoggerFactory.Instance);

        Assert.True(genericComputer.CPU.IsOpCodeDefined(StzAbsOpCode));
        Assert.Equal(3, genericComputer.CPU.GetOpCodeSize(StzAbsOpCode));
    }

    [Fact]
    public void GenericComputerSystemConfig_Rejects_65c02_With_Unofficial_Profile()
    {
        var systemConfig = new GenericComputerSystemConfig
        {
            CpuModelId = CpuModelIds.Ncr65c02,
            // Default profile is ExperimentalUnofficial -- unsupported on the 65C02.
        };

        Assert.False(systemConfig.IsValid(out var errors));
        Assert.Contains(errors, e => e.Contains("does not support compatibility profile"));
    }

    // ---- CpuModelInfo (the facade config UIs bind to) ----

    [Fact]
    public void CpuModelInfo_Lists_Both_Models_With_Display_Names()
    {
        Assert.Equal(new[] { CpuModelIds.Nmos6502, CpuModelIds.Ncr65c02 }, CpuModelInfo.AllModelIds);
        Assert.Equal("NMOS 6502", CpuModelInfo.GetDisplayName(CpuModelIds.Nmos6502));
        Assert.Equal("NCR 65C02", CpuModelInfo.GetDisplayName(CpuModelIds.Ncr65c02));
        Assert.True(CpuModelInfo.IsProfileSupported(CpuModelIds.Nmos6502, CpuCompatibilityProfile.FullUnofficial));
        Assert.False(CpuModelInfo.IsProfileSupported(CpuModelIds.Ncr65c02, CpuCompatibilityProfile.FullUnofficial));
    }
}
