using System.Reflection.Metadata;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Systems.Commodore64.Config;

public class C64Config : ISystemConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64";

    private bool _isDirty = false;
    public bool IsDirty => _isDirty;
    public void ClearDirty()
    {
        _isDirty = false;
    }

    public const string KERNAL_ROM_NAME = "kernal";
    public const string BASIC_ROM_NAME = "basic";
    public const string CHARGEN_ROM_NAME = "chargen";
    public static List<string> RequiredROMs = new()
    {
        KERNAL_ROM_NAME, BASIC_ROM_NAME, CHARGEN_ROM_NAME
    };

    public bool LoadROMs { get; set; } = true;
    private List<ROM> _roms;
    public List<ROM> ROMs
    {
        get
        {
            return _roms;
        }
        set
        {
            _roms = value;
            _isDirty = true;
        }
    }

    private string _romDirectory;
    public string ROMDirectory
    {
        get
        {
            return _romDirectory;
        }
        set
        {
            _romDirectory = value;
            _isDirty = true;
        }
    }

    private string _c64Model;
    public string C64Model
    {
        get
        {
            return _c64Model;
        }
        set
        {
            _c64Model = value;
            _isDirty = true;
        }
    }

    private string _vic2Model;
    public string Vic2Model
    {
        get
        {
            return _vic2Model;
        }
        set
        {
            _vic2Model = value;
            _isDirty = true;
        }
    }

    public TimerMode TimerMode { get; set; }

    public bool AudioSupported { get; set; }

    private bool _audioEnabled;
    public bool AudioEnabled
    {
        get
        {
            return _audioEnabled;
        }
        set
        {
            _audioEnabled = value;
            _isDirty = true;
        }
    }

    private string _colorMapName;
    public string ColorMapName
    {
        get
        {
            return _colorMapName;
        }
        set
        {
            _colorMapName = value;
            _isDirty = true;
        }
    }

    public bool KeyboardJoystickEnabled { get; set; }
    public C64KeyboardJoystickMap KeyboardJoystickMap { get; private set; }

    public C64Config()
    {
        // Defaults
        ROMs = new List<ROM>
        {
            new ROM
            {
                Name = BASIC_ROM_NAME,
                File = "basic",
                Data = null,
                Checksum = "79015323128650c742a3694c9429aa91f355905e",
            },
            new ROM
            {
                Name = CHARGEN_ROM_NAME,
                File = "chargen",
                Data = null,
                Checksum = "adc7c31e18c7c7413d54802ef2f4193da14711aa",
            },
            new ROM
            {
                Name = KERNAL_ROM_NAME,
                File = "kernal",
                Data = null,
                Checksum = "1d503e56df85a62fee696e7618dc5b4e781df1bb",
            },
        };
        ROMDirectory = "%USERPROFILE%/Documents/C64/VICE/C64";

        C64Model = "C64NTSC";
        Vic2Model = "NTSC";

        TimerMode = TimerMode.UpdateEachRasterLine;
        //TimerMode = TimerMode.UpdateEachInstruction;

        AudioSupported = false; // Set to true after creating if the audio system is implemented for the host platform
        AudioEnabled = false;

        ColorMapName = ColorMaps.DEFAULT_COLOR_MAP_NAME;

        KeyboardJoystickEnabled = true;
        KeyboardJoystickMap = new C64KeyboardJoystickMap();

    }

    public bool HasROM(string romName) => ROMs.Any(x => x.Name == romName);
    public ROM GetROM(string romName) => ROMs.Single(x => x.Name == romName);

    public void SetROM(string romName, string? file = null, byte[]? data = null)
    {
        if (HasROM(romName))
        {
            var rom = GetROM(romName);
            rom.File = file;
            rom.Data = data;
        }
        else
        {
            var rom = new ROM()
            {
                Name = romName,
                File = file,
                Data = data
            };
            ROMs.Add(rom);
        }

        _isDirty = true;
    }

    public C64Config Clone()
    {
        return new C64Config
        {
            ROMDirectory = ROMDirectory,
            C64Model = C64Model,
            Vic2Model = Vic2Model,
            ROMs = ROM.Clone(ROMs),
            TimerMode = TimerMode,
            AudioEnabled = AudioEnabled
        };
    }

    public void Validate()
    {
        if (!IsValid(out List<string> validationErrors))
            throw new Exception($"Config errors: {string.Join(',', validationErrors)}");
    }

    public bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();

        if (!C64ModelInventory.C64Models.ContainsKey(C64Model))
        {
            validationErrors.Add($"{nameof(C64Model)} value {C64Model} is not supported. Valid values are: {string.Join(',', C64ModelInventory.C64Models.Keys)}");
            return false;
        }

        var c64Model = C64ModelInventory.C64Models[C64Model];

        if (!c64Model.Vic2Models.Exists(x => x.Name == Vic2Model))
            validationErrors.Add($"{nameof(Vic2Model)} value {Vic2Model} is not supported for the specified C64Variant. Valid values are: {string.Join(',', c64Model.Vic2Models.Select(x => x.Name))}");

        var loadedRoms = ROMs.Select(x => x.Name).ToList();
        List<string> missingRoms = new();
        foreach (var romName in RequiredROMs)
        {
            if (!loadedRoms.Contains(romName))
                missingRoms.Add(romName);
        }
        if (missingRoms.Count > 0)
        {
            validationErrors.Add($"Missing ROMs: {string.Join(", ", missingRoms)}.");
        }

        var romDir = PathHelper.ExpandOSEnvironmentVariables(ROMDirectory);
        if (!string.IsNullOrEmpty(romDir))
        {
            if (!Directory.Exists(romDir))
                validationErrors.Add($"{nameof(ROMDirectory)} is not an existing directory: {romDir}");
        }
        foreach (var rom in ROMs)
        {
            var romValidationErrors = new List<string>();
            if (!rom.Validate(out romValidationErrors, ROMDirectory))
                validationErrors.AddRange(romValidationErrors);
        }

        return validationErrors.Count == 0;
    }
}

/// <summary>
/// How often the C64 CIA timers are updated. 
/// UpdateEachInstruction = more realistic, but affects performance.
/// UpdateEachRasterLine = less realistic, but better performance.
/// </summary>
public enum TimerMode
{
    UpdateEachInstruction,
    UpdateEachRasterLine
}
