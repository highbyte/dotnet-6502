using System.Text.Json;
using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Commodore64.Config;

/// <summary>
/// Snapshot config support for <see cref="C64SystemConfig"/>: the portable "system config" block of a
/// snapshot (see the config extension in the feature design doc). These settings live in the global
/// <c>C64SystemConfig</c> (same type on every host), so the block is portable across host apps.
///
/// <para>Applied to the config <i>before</i> the machine is rebuilt on restore, so the rebuilt C64
/// reflects them (e.g. <c>C64Joystick</c> reads <see cref="C64SystemConfig.KeyboardJoystickEnabled"/>
/// at build time) — no live-machine poke required. Add a portable setting = add a field to
/// <see cref="C64SystemSnapshotSettings"/> and map it below; the snapshot framework is untouched.</para>
/// </summary>
public partial class C64SystemConfig
{
    public string? ExportSnapshotSettings()
    {
        var settings = new C64SystemSnapshotSettings
        {
            AudioEnabled = AudioEnabled,
            KeyboardJoystickEnabled = KeyboardJoystickEnabled,
            KeyboardJoystick = KeyboardJoystick,
        };
        return JsonSerializer.Serialize(settings, C64SystemSnapshotSettingsJsonContext.Default.C64SystemSnapshotSettings);
    }

    public void ApplySnapshotSettings(string payload)
    {
        var settings = JsonSerializer.Deserialize(payload, C64SystemSnapshotSettingsJsonContext.Default.C64SystemSnapshotSettings);
        if (settings == null)
            return;
        // Applied to the config before the machine is rebuilt on load, so the rebuilt C64 picks these
        // up through the normal build/start path (e.g. audio is wired up when the audio pipeline is
        // initialized during Start, based on AudioEnabled).
        AudioEnabled = settings.AudioEnabled;
        KeyboardJoystickEnabled = settings.KeyboardJoystickEnabled;
        KeyboardJoystick = settings.KeyboardJoystick;
    }
}

/// <summary>Serialization schema for <see cref="C64SystemConfig"/>'s portable snapshot settings.</summary>
internal sealed class C64SystemSnapshotSettings
{
    public bool AudioEnabled { get; set; } = true;
    public bool KeyboardJoystickEnabled { get; set; }
    public int KeyboardJoystick { get; set; } = 2;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(C64SystemSnapshotSettings))]
internal partial class C64SystemSnapshotSettingsJsonContext : JsonSerializerContext
{
}
