using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.Config;

public class C64SystemConfig : ISystemConfig
{
    private bool _isDirty = false;
    [JsonIgnore]
    public bool IsDirty => _isDirty;
    public void ClearDirty()
    {
        _isDirty = false;
    }

    // TODO: Decide if DefaultKernalROMChecksums should exists here in C64SystemConfig, C64Config, or in C64
    // ROM version info from: https://www.commodore.ca/manuals/funet/cbm/firmware/computers/c64/
    // Checksums calculated with SHA1
    public const string KERNAL_ROM_NAME = "kernal";
    public static Dictionary<string, string> DefaultKernalROMChecksums = new()
    {
        // Commodore 64 KERNAL ROM Revision 1. The RS-232 timing table is designed for exactly 1 MHz system clock frequency, although no C64 runs at that clock rate. Ripped from a very old American C64.
        { "901227-01", "87cc04d61fc748b82df09856847bb5c2754a2033" },

        // Commodore 64 KERNAL ROM Revision 2. Can be found on 1982 and 1983 models.
        { "901227-02", "0e2e4ee3f2d41f00bed72f9ab588b83e306fdb13" },

        // ! RECOMENDED ! Commodore 64 KERNAL ROM Revision 3. The last revision, also used in the C128's C64 mode.
        { "901227-03", "1d503e56df85a62fee696e7618dc5b4e781df1bb" },

        // Commodore 64 KERNAL ROM Revision 3, patched for Swedish/Finnish keyboard layout.
        { "swedish", "e4f52d9b36c030eb94524eb49f6f0774c1d02e5e" },

        // Commodore PET64 or 4064 KERNAL. With black&white startup colors, and with a different bootup message. Machines with color monitors used the standard Commodore 64 KERNAL ROM.
        { "4064.901246-01", "6c4fa9465f6091b174df27dfe679499df447503c" },
    };
    public const string BASIC_ROM_NAME = "basic";
    public static Dictionary<string, string> DefaultBasicROMChecksums = new()
    {
        // Commodore 64 BASIC V2. The first and only revision.
        { "901226-01", "79015323128650c742a3694c9429aa91f355905e" }
    };
    public const string CHARGEN_ROM_NAME = "chargen";
    public static Dictionary<string, string> DefaultCharGenROMChecksums = new()
    {
        // The character generator ROM.
        { "901225-01", "adc7c31e18c7c7413d54802ef2f4193da14711aa" }
    };

    public static List<string> RequiredROMs = new()
    {
        KERNAL_ROM_NAME, BASIC_ROM_NAME, CHARGEN_ROM_NAME
    };

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

    private bool _audioEnabled;
    public bool AudioEnabled
    {
        get => _audioEnabled;
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

    private bool _keyboardJoystickEnabled;
    public bool KeyboardJoystickEnabled
    {
        get
        {
            return _keyboardJoystickEnabled;
        }
        set
        {
            _keyboardJoystickEnabled = value;
            _isDirty = true;
        }
    }

    private int _keyboardJoystick;
    public int KeyboardJoystick
    {
        get
        {
            return _keyboardJoystick;
        }
        set
        {
            _keyboardJoystick = value;
            _isDirty = true;
        }
    }

    [JsonIgnore]
    public C64KeyboardJoystickMap KeyboardJoystickMap { get; private set; }

    public C64SystemConfig()
    {
        // Defaults
        _roms = new List<ROM>();
        _romDirectory = "%USERPROFILE%/Documents/C64/VICE/C64";

        _colorMapName = ColorMaps.DEFAULT_COLOR_MAP_NAME;

        _audioEnabled = false;
        _keyboardJoystickEnabled = false;
        _keyboardJoystick = 2;

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

            SetROMDefaultCheckum(rom);

            ROMs.Add(rom);
        }

        _isDirty = true;
    }

    private void SetROMDefaultCheckum(ROM rom)
    {
        // If checksum(s) is already set (from config file), skip setting default checksum(s).
        if (rom.ValidVersionChecksums.Count != 0)
            return;

        // Set default checksums for known ROMs.
        if (rom.Name == BASIC_ROM_NAME)
        {
            rom.ValidVersionChecksums = DefaultBasicROMChecksums;
        }
        else if (rom.Name == KERNAL_ROM_NAME)
        {
            rom.ValidVersionChecksums = DefaultKernalROMChecksums;
        }
        else if (rom.Name == CHARGEN_ROM_NAME)
        {
            rom.ValidVersionChecksums = DefaultCharGenROMChecksums;
        }
    }

    public object Clone()
    {
        var clone = (C64SystemConfig)this.MemberwiseClone();
        clone.ROMs = ROM.Clone(ROMs);
        return clone;
    }

    public void Validate()
    {
        if (!IsValid(out List<string> validationErrors))
            throw new DotNet6502Exception($"Config errors: {string.Join(',', validationErrors)}");
    }

    public bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();

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
        // Skip other ROM validation if the ROM directory is not valid.
        if (validationErrors.Count == 0)
        {
            foreach (var rom in ROMs)
            {
                var romValidationErrors = new List<string>();
                if (!rom.Validate(out romValidationErrors, ROMDirectory))
                    validationErrors.AddRange(romValidationErrors);
            }
        }

        return validationErrors.Count == 0;
    }
}
