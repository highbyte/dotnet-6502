using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public class Apple2SystemConfigSnapshotTests
{
    [Fact]
    public void Portable_settings_round_trip_through_export_and_apply()
    {
        // Every value is the opposite of what a fresh config starts with, so a field the payload
        // silently dropped would leave the target on its default and fail the assertions below.
        // Note AudioEnabled is captured as false: it defaults to true, so capturing true here would
        // pass whether or not the field actually travelled.
        var source = new Apple2SystemConfig
        {
            AudioEnabled = false,
            KeyboardJoystickEnabled = true,
            MonitorColor = Apple2MonitorColor.Green,
        };

        var json = ((ISnapshotableConfig)source).ExportSnapshotSettings();
        Assert.False(string.IsNullOrEmpty(json));

        var target = new Apple2SystemConfig();
        Assert.True(target.AudioEnabled);
        Assert.False(target.KeyboardJoystickEnabled);
        Assert.Equal(Apple2MonitorColor.Color, target.MonitorColor);

        ((ISnapshotableConfig)target).ApplySnapshotSettings(json!);

        Assert.False(target.AudioEnabled);
        Assert.True(target.KeyboardJoystickEnabled);
        Assert.Equal(Apple2MonitorColor.Green, target.MonitorColor);
    }

    [Fact]
    public void Applying_a_payload_with_unknown_fields_is_tolerated()
    {
        var config = new Apple2SystemConfig();

        // A snapshot written by a newer build may carry settings this one has never heard of.
        // Fields the payload omits fall back to the schema's defaults, which mirror a fresh config.
        ((ISnapshotableConfig)config).ApplySnapshotSettings("{\"somethingElse\":123}");

        Assert.True(config.AudioEnabled);
        Assert.Equal(Apple2MonitorColor.Color, config.MonitorColor);
    }
}
