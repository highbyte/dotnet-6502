using Highbyte.DotNet6502.Systems.Oric.Config;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public sealed class OricSystemConfigSnapshotTests
{
    [Fact]
    public void Portable_settings_round_trip_through_export_and_apply()
    {
        var source = new OricSystemConfig
        {
            AudioEnabled = false,
            VSyncHackEnabled = true,
            JoystickInterface = OricJoystickInterface.IJK,
            KeyboardJoystickEnabled = true,
            KeyboardJoystick = 2,
            CpuCompatibilityProfile = CpuCompatibilityProfile.FullUnofficial,
        };

        var json = ((ISnapshotableConfig)source).ExportSnapshotSettings();
        Assert.False(string.IsNullOrEmpty(json));

        var target = new OricSystemConfig();
        ((ISnapshotableConfig)target).ApplySnapshotSettings(json!);

        Assert.False(target.AudioEnabled);
        Assert.True(target.VSyncHackEnabled);
        Assert.Equal(OricJoystickInterface.IJK, target.JoystickInterface);
        Assert.True(target.KeyboardJoystickEnabled);
        Assert.Equal(2, target.KeyboardJoystick);
        Assert.Equal(CpuCompatibilityProfile.FullUnofficial, target.CpuCompatibilityProfile);
    }

    [Fact]
    public void Applying_a_payload_with_unknown_fields_uses_portable_defaults()
    {
        var config = new OricSystemConfig
        {
            AudioEnabled = false,
            VSyncHackEnabled = true,
            JoystickInterface = OricJoystickInterface.PASE,
            KeyboardJoystickEnabled = true,
            KeyboardJoystick = 2,
            CpuCompatibilityProfile = CpuCompatibilityProfile.FullUnofficial,
        };

        ((ISnapshotableConfig)config).ApplySnapshotSettings("{\"somethingElse\":123}");

        Assert.True(config.AudioEnabled);
        Assert.False(config.VSyncHackEnabled);
        Assert.Equal(OricJoystickInterface.None, config.JoystickInterface);
        Assert.False(config.KeyboardJoystickEnabled);
        Assert.Equal(1, config.KeyboardJoystick);
        Assert.Equal(CpuCompatibilityProfile.StableUnofficial, config.CpuCompatibilityProfile);
    }
}
