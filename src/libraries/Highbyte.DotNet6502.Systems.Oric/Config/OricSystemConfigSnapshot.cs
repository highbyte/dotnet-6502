using System.Text.Json;
using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems.Oric.Input;

namespace Highbyte.DotNet6502.Systems.Oric.Config;

/// <summary>Portable Oric settings optionally carried in a snapshot config block.</summary>
public sealed partial class OricSystemConfig
{
    public string? ExportSnapshotSettings()
    {
        var settings = new OricSystemSnapshotSettings
        {
            AudioEnabled = AudioEnabled,
            VSyncHackEnabled = VSyncHackEnabled,
            JoystickInterface = JoystickInterface,
            KeyboardJoystickEnabled = KeyboardJoystickEnabled,
            KeyboardJoystick = KeyboardJoystick,
            CpuCompatibilityProfile = CpuCompatibilityProfile,
        };
        return JsonSerializer.Serialize(
            settings,
            OricSystemSnapshotSettingsJsonContext.Default.OricSystemSnapshotSettings);
    }

    public void ApplySnapshotSettings(string payload)
    {
        var settings = JsonSerializer.Deserialize(
            payload,
            OricSystemSnapshotSettingsJsonContext.Default.OricSystemSnapshotSettings);
        if (settings is null)
            return;

        AudioEnabled = settings.AudioEnabled;
        VSyncHackEnabled = settings.VSyncHackEnabled;
        JoystickInterface = settings.JoystickInterface;
        KeyboardJoystickEnabled = settings.KeyboardJoystickEnabled;
        KeyboardJoystick = settings.KeyboardJoystick;
        CpuCompatibilityProfile = settings.CpuCompatibilityProfile;
    }
}

internal sealed class OricSystemSnapshotSettings
{
    public bool AudioEnabled { get; set; } = true;
    public bool VSyncHackEnabled { get; set; }
    public OricJoystickInterface JoystickInterface { get; set; } = OricJoystickInterface.None;
    public bool KeyboardJoystickEnabled { get; set; }
    public int KeyboardJoystick { get; set; } = 1;
    public CpuCompatibilityProfile CpuCompatibilityProfile { get; set; } = CpuCompatibilityProfile.StableUnofficial;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OricSystemSnapshotSettings))]
internal partial class OricSystemSnapshotSettingsJsonContext : JsonSerializerContext
{
}
