using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems.Configuration;
using Highbyte.DotNet6502.Systems.Oric.Audio;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Highbyte.DotNet6502.Systems.Oric.Render;
using Highbyte.DotNet6502.Utils;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Oric.Config;

/// <summary>User-facing configuration for the Oric family.</summary>
public sealed class OricSystemConfig : ISystemConfig
{
    public const string SystemRomName = "basic";
    public const string RomSourceInfoUrl = "https://abdess.github.io/retrobios/emulators/oricutron/";
    public const string DefaultSystemRomDownloadUrl =
        "https://raw.githubusercontent.com/Abdess/retrobios/main/bios/Oric/Oric/basic11b.rom";
    public const string AtmosRomFileName = "basic11b.rom";
    public const string AtmosRomSha1 = "9451a1a09d8f75944dbd6f91193fc360f1de80ac";

    public static readonly IReadOnlyList<string> RequiredRoms = [SystemRomName];

    private bool _isDirty;
    private string _romDirectory = string.Empty;
    private List<ROM> _roms = [];
    private CpuCompatibilityProfile _cpuCompatibilityProfile = CpuCompatibilityProfile.StableUnofficial;
    private bool _audioEnabled = true;
    private bool _vSyncHackEnabled;
    private OricJoystickInterface _joystickInterface = OricJoystickInterface.None;
    private bool _keyboardJoystickEnabled;
    private int _keyboardJoystick = 1;

    [JsonIgnore]
    public bool IsDirty => _isDirty;

    [JsonIgnore]
    public Type? RenderProviderType { get; private set; }

    [JsonPropertyName("RenderProviderType")]
    public string? RenderProviderTypeName
    {
        get => ConfiguredTypeName.Format(RenderProviderType);
        set => SetRenderProviderType(ConfiguredTypeName.Resolve(value));
    }

    [JsonIgnore]
    public Type? RenderTargetType { get; private set; }

    [JsonPropertyName("RenderTargetType")]
    public string? RenderTargetTypeName
    {
        get => ConfiguredTypeName.Format(RenderTargetType);
        set => SetRenderTargetType(ConfiguredTypeName.Resolve(value));
    }

    [JsonIgnore]
    public Type? AudioProviderType { get; private set; }

    [JsonPropertyName("AudioProviderType")]
    public string? AudioProviderTypeName
    {
        get => ConfiguredTypeName.Format(AudioProviderType);
        set => SetAudioProviderType(ConfiguredTypeName.Resolve(value));
    }

    [JsonIgnore]
    public Type? AudioTargetType { get; private set; }

    [JsonPropertyName("AudioTargetType")]
    public string? AudioTargetTypeName
    {
        get => ConfiguredTypeName.Format(AudioTargetType);
        set => SetAudioTargetType(ConfiguredTypeName.Resolve(value));
    }

    public bool AudioEnabled
    {
        get => _audioEnabled;
        set { _audioEnabled = value; _isDirty = true; }
    }

    /// <summary>
    /// Emulates the common hardware modification that routes the RGB VSync signal to the
    /// cassette-input CB1 pin. Timing-sensitive software can use it as a frame interrupt.
    /// </summary>
    public bool VSyncHackEnabled
    {
        get => _vSyncHackEnabled;
        set { _vSyncHackEnabled = value; _isDirty = true; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<OricJoystickInterface>))]
    public OricJoystickInterface JoystickInterface
    {
        get => _joystickInterface;
        set { _joystickInterface = value; _isDirty = true; }
    }

    public bool KeyboardJoystickEnabled
    {
        get => _keyboardJoystickEnabled;
        set { _keyboardJoystickEnabled = value; _isDirty = true; }
    }

    public int KeyboardJoystick
    {
        get => _keyboardJoystick;
        set { _keyboardJoystick = value; _isDirty = true; }
    }

    public CpuCompatibilityProfile CpuCompatibilityProfile
    {
        get => _cpuCompatibilityProfile;
        set { _cpuCompatibilityProfile = value; _isDirty = true; }
    }

    public string ROMDirectory
    {
        get => _romDirectory;
        set { _romDirectory = value ?? string.Empty; _isDirty = true; }
    }

    [JsonIgnore]
    public static string DefaultROMDirectory => AppStoragePaths.GetRomDirectory(OricMachine.SystemName);

    [JsonIgnore]
    public string EffectiveROMDirectory => string.IsNullOrWhiteSpace(ROMDirectory) && !OperatingSystem.IsBrowser()
        ? DefaultROMDirectory
        : ROMDirectory;

    [JsonIgnore]
    public Dictionary<string, RomDownloadSource> ROMDownloadSources { get; } = new()
    {
        [SystemRomName] = new(DefaultSystemRomDownloadUrl, FileName: AtmosRomFileName),
    };

    public Dictionary<string, string> ROMDownloadUrls =>
        ROMDownloadSources.ToDictionary(entry => entry.Key, entry => entry.Value.Url);

    public List<ROM> ROMs
    {
        get => _roms;
        set
        {
            _roms = value ?? [];
            foreach (var rom in _roms)
                ApplyDefaultChecksums(rom);
            _isDirty = true;
        }
    }

    public OricSystemConfig()
    {
        SetRenderProviderType(typeof(OricRasterizer));
        SetAudioProviderType(typeof(OricAySampleProvider));
        _isDirty = false;
    }

    public bool HasROM(string romName) => _roms.Any(rom => rom.Name == romName);

    public ROM GetROM(string romName) => _roms.Single(rom => rom.Name == romName);

    public void SetROM(string romName, string? file = null, byte[]? data = null)
    {
        if (HasROM(romName))
        {
            var rom = GetROM(romName);
            rom.File = file;
            rom.Data = data;
            ApplyDefaultChecksums(rom);
        }
        else
        {
            var rom = new ROM { Name = romName, File = file, Data = data };
            ApplyDefaultChecksums(rom);
            _roms.Add(rom);
        }
        _isDirty = true;
    }

    private static void ApplyDefaultChecksums(ROM rom)
    {
        if (rom.Name != SystemRomName || rom.ValidVersionChecksums.Count != 0)
            return;
        rom.ValidVersionChecksums = new Dictionary<string, string>
        {
            ["Oric Atmos BASIC 1.1b, 16 KB"] = AtmosRomSha1,
        };
    }

    public List<Type> GetSupportedRenderProviderTypes() =>
    [
        typeof(OricRasterizer),
        typeof(OricVideoCommandStream),
    ];

    public void SetRenderProviderType(Type? renderProviderType)
    {
        if (renderProviderType != null && !GetSupportedRenderProviderTypes().Contains(renderProviderType))
            throw new DotNet6502Exception($"Unsupported Oric render provider: {renderProviderType.FullName}");
        RenderProviderType = renderProviderType;
        _isDirty = true;
    }

    public void SetRenderTargetType(Type? renderTargetType)
    {
        RenderTargetType = renderTargetType;
        _isDirty = true;
    }

    public List<Type> GetSupportedAudioProviderTypes() => [typeof(OricAySampleProvider)];

    public void SetAudioProviderType(Type? audioProviderType)
    {
        if (audioProviderType != null && !GetSupportedAudioProviderTypes().Contains(audioProviderType))
            throw new DotNet6502Exception($"Unsupported Oric audio provider: {audioProviderType.FullName}");
        AudioProviderType = audioProviderType;
        _isDirty = true;
    }

    public void SetAudioTargetType(Type? audioTargetType)
    {
        AudioTargetType = audioTargetType;
        _isDirty = true;
    }

    public void ClearDirty() => _isDirty = false;

    public object Clone()
    {
        var clone = (OricSystemConfig)MemberwiseClone();
        clone._roms = ROM.Clone(_roms);
        return clone;
    }

    public void Validate()
    {
        if (!IsValid(out var validationErrors))
            throw new DotNet6502Exception($"Config errors: {string.Join(", ", validationErrors)}");
    }

    public bool IsValid(out List<string> validationErrors)
    {
        validationErrors = [];

        if (!CpuModelInfo.IsProfileSupported(CpuModelIds.Nmos6502, CpuCompatibilityProfile))
            validationErrors.Add($"NMOS 6502 does not support compatibility profile '{CpuCompatibilityProfile}'.");

        if (!Enum.IsDefined(JoystickInterface))
            validationErrors.Add($"Unsupported Oric joystick interface '{JoystickInterface}'.");

        if (KeyboardJoystick is not (1 or 2))
            validationErrors.Add($"{nameof(KeyboardJoystick)} must be 1 or 2.");

        var loadedNames = _roms.Select(rom => rom.Name).ToHashSet();
        var missing = RequiredRoms.Where(required => !loadedNames.Contains(required)).ToList();
        if (missing.Count != 0)
            validationErrors.Add($"Missing ROMs: {string.Join(", ", missing)}.");

        var romDir = PathHelper.ExpandOSEnvironmentVariables(EffectiveROMDirectory);
        if (_roms.Any(rom => !string.IsNullOrEmpty(rom.File)) && !string.IsNullOrEmpty(romDir) && !Directory.Exists(romDir))
            validationErrors.Add($"{nameof(ROMDirectory)} does not exist: {romDir}");

        if (validationErrors.Count == 0)
        {
            foreach (var rom in _roms)
            {
                if (!rom.Validate(out var romErrors, EffectiveROMDirectory))
                    validationErrors.AddRange(romErrors);
            }
        }

        return validationErrors.Count == 0;
    }
}
