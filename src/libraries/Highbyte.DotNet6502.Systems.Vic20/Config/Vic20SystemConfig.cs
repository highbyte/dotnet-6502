using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Utils;
using Highbyte.DotNet6502.Systems.Vic20.Render;

namespace Highbyte.DotNet6502.Systems.Vic20.Config;

public class Vic20SystemConfig : ISystemConfig
{
    public const string BASIC_ROM_NAME = "basic";
    public const string KERNAL_ROM_NAME = "kernal";
    public const string CHARGEN_ROM_NAME = "chargen";

    public static readonly List<string> RequiredROMs = new() { BASIC_ROM_NAME, KERNAL_ROM_NAME, CHARGEN_ROM_NAME };

    // SHA1 checksums for known VIC-20 ROM versions (version label → sha1 hex, lowercase)
    public static Dictionary<string, string> DefaultBasicROMChecksums = new()
    {
        { "901486-01", "587d1e90950675ab6b12d91248a3f0d640d02e8d" },
    };
    public static Dictionary<string, string> DefaultKernalROMChecksums = new()
    {
        { "901486-07 (PAL-B)", "ce0137ed69f003a299f43538fa9eee27898e621e" },
    };
    public static Dictionary<string, string> DefaultChargenROMChecksums = new()
    {
        { "901460-03", "4fd85ab6647ee2ac7ba40f729323f2472d35b9b4" },
    };

    private bool _isDirty = false;
    public bool IsDirty => _isDirty;

    [JsonIgnore]
    public Type? RenderProviderType { get; private set; }

    [JsonPropertyName("RenderProviderType")]
    public string? RenderProviderTypeName
    {
        get => RenderProviderType?.AssemblyQualifiedName;
        set => SetRenderProviderType(value != null ? Type.GetType(value) : null);
    }

    [JsonIgnore]
    public Type? RenderTargetType { get; private set; }

    [JsonPropertyName("RenderTargetType")]
    public string? RenderTargetTypeName
    {
        get => RenderTargetType?.AssemblyQualifiedName;
        set => SetRenderTargetType(value != null ? Type.GetType(value) : null);
    }

    public bool AudioEnabled { get; set; } = false;

    private string _romDirectory = string.Empty;
    public string ROMDirectory
    {
        get => _romDirectory;
        set { _romDirectory = value; _isDirty = true; }
    }

    private List<ROM> _roms = new();
    public List<ROM> ROMs
    {
        get => _roms;
        set
        {
            _roms = value;
            foreach (var rom in _roms)
                ApplyDefaultChecksums(rom);
            _isDirty = true;
        }
    }

    private void ApplyDefaultChecksums(ROM rom)
    {
        if (rom.ValidVersionChecksums.Count != 0)
            return;
        if (rom.Name == BASIC_ROM_NAME)
            rom.ValidVersionChecksums = new Dictionary<string, string>(DefaultBasicROMChecksums);
        else if (rom.Name == KERNAL_ROM_NAME)
            rom.ValidVersionChecksums = new Dictionary<string, string>(DefaultKernalROMChecksums);
        else if (rom.Name == CHARGEN_ROM_NAME)
            rom.ValidVersionChecksums = new Dictionary<string, string>(DefaultChargenROMChecksums);
    }

    public bool HasROM(string romName) => _roms.Any(x => x.Name == romName);
    public ROM GetROM(string romName) => _roms.Single(x => x.Name == romName);

    [JsonIgnore]
    public Type? AudioProviderType => null;

    [JsonIgnore]
    public Type? AudioTargetType => null;

    public List<Type> GetSupportedRenderProviderTypes() =>
        new() { typeof(Vic20VideoCommandStream) };

    public List<Type> GetSupportedAudioProviderTypes() => new();

    public void SetRenderProviderType(Type? renderProviderType)
    {
        if (renderProviderType == null)
        {
            RenderProviderType = null;
            return;
        }
        if (!GetSupportedRenderProviderTypes().Contains(renderProviderType))
            throw new DotNet6502Exception($"Unsupported render provider: {renderProviderType.FullName}");
        RenderProviderType = renderProviderType;
    }

    public void SetRenderTargetType(Type? renderTargetType)
    {
        RenderTargetType = renderTargetType;
        _isDirty = true;
    }

    public void SetAudioProviderType(Type? audioProviderType)
    {
        if (audioProviderType != null)
            throw new DotNet6502Exception("VIC-20 has no audio providers in this stub.");
    }

    public void SetAudioTargetType(Type? audioTargetType)
    {
        if (audioTargetType != null)
            throw new DotNet6502Exception("VIC-20 has no audio targets in this stub.");
    }

    public Vic20SystemConfig()
    {
        SetRenderProviderType(GetSupportedRenderProviderTypes().First());
    }

    public void ClearDirty() => _isDirty = false;

    public object Clone()
    {
        var clone = (Vic20SystemConfig)MemberwiseClone();
        clone._roms = ROM.Clone(_roms);
        return clone;
    }

    public void Validate()
    {
        if (!IsValid(out var errors))
            throw new DotNet6502Exception($"Config errors: {string.Join(", ", errors)}");
    }

    public bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();

        var loadedNames = _roms.Select(x => x.Name).ToList();
        var missing = RequiredROMs.Where(r => !loadedNames.Contains(r)).ToList();
        if (missing.Count > 0)
            validationErrors.Add($"Missing ROMs: {string.Join(", ", missing)}.");

        var romDir = PathHelper.ExpandOSEnvironmentVariables(_romDirectory);
        if (!string.IsNullOrEmpty(romDir) && !Directory.Exists(romDir))
            validationErrors.Add($"{nameof(ROMDirectory)} does not exist: {romDir}");

        if (validationErrors.Count == 0)
        {
            foreach (var rom in _roms)
            {
                if (!rom.Validate(out var romErrors, _romDirectory))
                    validationErrors.AddRange(romErrors);
            }
        }

        return validationErrors.Count == 0;
    }
}
