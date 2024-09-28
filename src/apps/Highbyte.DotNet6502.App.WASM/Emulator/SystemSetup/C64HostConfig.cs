using System.Text.Json.Serialization;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.WASM.Emulator.SystemSetup;

[JsonConverter(typeof(JsonStringEnumConverter<C64HostRenderer>))]
public enum C64HostRenderer
{
    SkiaSharp,
    SkiaSharp2,  // Experimental render directly to pixel buffer backed by a SKBitmap + Skia shader (SKSL)
    SkiaSharp2b, // Experimental render after each instruction directly to pixel buffer backed by a SKBitmap + Skia shader (SKSL)
}
public class C64HostConfig : IHostSystemConfig, ICloneable
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64HostConfig";

    private bool _isDirty = false;

    [JsonIgnore]
    public bool IsDirty => _isDirty;
    public void ClearDirty()
    {
        _isDirty = false;
    }

    private List<ROM> _roms = default!;
    public List<ROM> ROMs
    {
        get
        {
            return _roms;
        }
        set
        {
            _roms = value;
            foreach (var rom in _roms)
            {
                SetROMDefaultCheckum(rom);
            }
            _isDirty = true;
        }
    }

    public C64HostRenderer Renderer { get; set; } = C64HostRenderer.SkiaSharp;

    public C64AspNetInputConfig InputConfig { get; set; } = new C64AspNetInputConfig();

    private bool _basicAIAssistantDefaultEnabled;
    [JsonIgnore]
    public bool BasicAIAssistantDefaultEnabled
    {
        get => _basicAIAssistantDefaultEnabled;
        set
        {
            _basicAIAssistantDefaultEnabled = value;
            _isDirty = true;
        }
    }

    private CodeSuggestionBackendTypeEnum _codeSuggestionBackendType;
    public CodeSuggestionBackendTypeEnum CodeSuggestionBackendType
    {
        get => _codeSuggestionBackendType;
        set
        {
            _codeSuggestionBackendType = value;
            _isDirty = true;
        }
    }

    public C64HostConfig()
    {
        // Defaults
        _roms = new List<ROM>();

        BasicAIAssistantDefaultEnabled = false;
        CodeSuggestionBackendType = CodeSuggestionBackendTypeEnum.CustomEndpoint;
    }

    public void ApplySettingsToSystemConfig(ISystemConfig systemConfig)
    {
        var c64SystemConfig = (C64Config)systemConfig;
        c64SystemConfig.ROMs = ROMs;
        c64SystemConfig.ROMDirectory = "";
    }

    public bool HasROM(string romName) => ROMs.Any(x => x.Name == romName);
    public ROM GetROM(string romName) => ROMs.Single(x => x.Name == romName);

    public void SetROM(string romName, string? file = null, byte[]? data = null)
    {
        ROM rom;
        if (HasROM(romName))
        {
            rom = GetROM(romName);
            rom.File = file;
            rom.Data = data;
        }
        else
        {
            rom = new ROM()
            {
                Name = romName,
                File = file,
                Data = data
            };

            ROMs.Add(rom);
        }

        SetROMDefaultCheckum(rom);

        _isDirty = true;
    }

    private void SetROMDefaultCheckum(ROM rom)
    {
        // If checksum(s) is already set (from config file), skip setting default checksum(s).
        if (rom.ValidVersionChecksums.Count != 0)
            return;

        // Set default checksums for known ROMs.
        if (rom.Name == C64Config.BASIC_ROM_NAME)
        {
            rom.ValidVersionChecksums = C64Config.DefaultBasicROMChecksums;
        }
        else if (rom.Name == C64Config.KERNAL_ROM_NAME)
        {
            rom.ValidVersionChecksums = C64Config.DefaultKernalROMChecksums;
        }
        else if (rom.Name == C64Config.CHARGEN_ROM_NAME)
        {
            rom.ValidVersionChecksums = C64Config.DefaultCharGenROMChecksums;
        }
    }

    public object Clone()
    {
        var clone = (C64HostConfig)MemberwiseClone();
        clone.InputConfig = (C64AspNetInputConfig)InputConfig.Clone();
        clone.ROMs = ROM.Clone(ROMs);
        return clone;
    }
}
