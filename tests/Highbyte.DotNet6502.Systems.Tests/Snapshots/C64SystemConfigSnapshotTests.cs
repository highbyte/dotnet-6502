using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public class C64SystemConfigSnapshotTests
{
    [Fact]
    public void C64SystemConfig_exports_and_applies_portable_settings()
    {
        // Arrange: a config with non-default portable settings.
        var source = new C64SystemConfig
        {
            AudioEnabled = false,
            KeyboardJoystickEnabled = true,
            KeyboardJoystick = 1,
        };

        // Act: export via ISnapshotableConfig, then apply into a fresh config.
        var json = ((ISnapshotableConfig)source).ExportSnapshotSettings();
        Assert.False(string.IsNullOrEmpty(json));

        var target = new C64SystemConfig
        {
            AudioEnabled = true,
            KeyboardJoystickEnabled = false,
            KeyboardJoystick = 2,
        };
        ((ISnapshotableConfig)target).ApplySnapshotSettings(json!);

        // Assert: the portable settings were transferred.
        Assert.False(target.AudioEnabled);
        Assert.True(target.KeyboardJoystickEnabled);
        Assert.Equal(1, target.KeyboardJoystick);
    }

    [Fact]
    public void C64SystemConfig_apply_tolerates_unrelated_or_partial_json()
    {
        var config = new C64SystemConfig { KeyboardJoystickEnabled = true, KeyboardJoystick = 2 };
        // Unknown fields ignored; missing fields fall back to the DTO defaults.
        ((ISnapshotableConfig)config).ApplySnapshotSettings("{\"somethingElse\":123}");
        // keyboardJoystickEnabled defaults to false, keyboardJoystick to 2 (DTO default) when absent.
        Assert.False(config.KeyboardJoystickEnabled);
        Assert.Equal(2, config.KeyboardJoystick);
    }
}
