using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public class Apple2SystemConfigSnapshotTests
{
    [Fact]
    public void Portable_settings_round_trip_through_export_and_apply()
    {
        var source = new Apple2SystemConfig
        {
            AudioEnabled = true,
            KeyboardJoystickEnabled = true,
            MonitorColor = Apple2MonitorColor.Green,
        };

        var json = ((ISnapshotableConfig)source).ExportSnapshotSettings();
        Assert.False(string.IsNullOrEmpty(json));

        var target = new Apple2SystemConfig();
        // Start the target on the opposite of every captured value, so an ignored field fails.
        Assert.False(target.AudioEnabled);
        Assert.False(target.KeyboardJoystickEnabled);
        Assert.Equal(Apple2MonitorColor.Color, target.MonitorColor);

        ((ISnapshotableConfig)target).ApplySnapshotSettings(json!);

        Assert.True(target.AudioEnabled);
        Assert.True(target.KeyboardJoystickEnabled);
        Assert.Equal(Apple2MonitorColor.Green, target.MonitorColor);
    }

    [Fact]
    public void Applying_a_payload_with_unknown_fields_is_tolerated()
    {
        var config = new Apple2SystemConfig();

        // A snapshot written by a newer build may carry settings this one has never heard of.
        ((ISnapshotableConfig)config).ApplySnapshotSettings("{\"somethingElse\":123}");

        Assert.False(config.AudioEnabled);
        Assert.Equal(Apple2MonitorColor.Color, config.MonitorColor);
    }
}
