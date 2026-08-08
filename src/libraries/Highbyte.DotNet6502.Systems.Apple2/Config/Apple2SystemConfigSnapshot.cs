using System.Text.Json;
using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems.Apple2.Video;

namespace Highbyte.DotNet6502.Systems.Apple2.Config;

/// <summary>
/// Snapshot config support for <see cref="Apple2SystemConfig"/>: the portable "system config" block of
/// a snapshot. These settings live in the global <c>Apple2SystemConfig</c> (the same type on every
/// host), so the block is portable across host apps.
///
/// <para>Applied to the config <i>before</i> the machine is rebuilt on restore, which is what makes
/// these particular settings worth carrying: all three are read at build or init time rather than
/// polled as live toggles. The audio provider is only constructed if <c>AudioEnabled</c> is set when
/// the machine is built, the game port is only driven by host keys if
/// <c>KeyboardJoystickEnabled</c> is set, and the monitor colour is baked into the rasterizer's
/// palette per frame. Add a portable setting = add a field to
/// <see cref="Apple2SystemSnapshotSettings"/> and map it below; the snapshot framework is
/// untouched.</para>
/// </summary>
public partial class Apple2SystemConfig
{
    public string? ExportSnapshotSettings()
    {
        var settings = new Apple2SystemSnapshotSettings
        {
            AudioEnabled = AudioEnabled,
            KeyboardJoystickEnabled = KeyboardJoystickEnabled,
            MonitorColor = MonitorColor,
        };
        return JsonSerializer.Serialize(settings, Apple2SystemSnapshotSettingsJsonContext.Default.Apple2SystemSnapshotSettings);
    }

    public void ApplySnapshotSettings(string payload)
    {
        var settings = JsonSerializer.Deserialize(payload, Apple2SystemSnapshotSettingsJsonContext.Default.Apple2SystemSnapshotSettings);
        if (settings == null)
            return;

        AudioEnabled = settings.AudioEnabled;
        KeyboardJoystickEnabled = settings.KeyboardJoystickEnabled;
        MonitorColor = settings.MonitorColor;
    }
}

/// <summary>Serialization schema for <see cref="Apple2SystemConfig"/>'s portable snapshot settings.</summary>
internal sealed class Apple2SystemSnapshotSettings
{
    /// <summary>Defaults mirror <see cref="Apple2SystemConfig"/>'s own, so a payload that omits a
    /// field leaves the setting where a fresh config would put it.</summary>
    public bool AudioEnabled { get; set; }

    public bool KeyboardJoystickEnabled { get; set; }

    public Apple2MonitorColor MonitorColor { get; set; } = Apple2MonitorColor.Color;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Apple2SystemSnapshotSettings))]
internal partial class Apple2SystemSnapshotSettingsJsonContext : JsonSerializerContext
{
}
