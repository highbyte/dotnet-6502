using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems.Apple2.Render;
using Highbyte.DotNet6502.Systems.Apple2.Audio.Sample;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Configuration;
using Highbyte.DotNet6502.Systems.Snapshots;
using Highbyte.DotNet6502.Utils;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2.Config;

/// <summary>
/// User-facing Apple II configuration: ROM files and display preferences.
/// </summary>
public partial class Apple2SystemConfig : ISystemConfig, ISnapshotableConfig
{
    /// <summary>
    /// The combined Applesoft BASIC + Autostart Monitor ROM mapped at $D000-$FFFF.
    /// </summary>
    public const string SYSTEM_ROM_NAME = "apple2";

    /// <summary>
    /// The 2513 character generator, holding the 64 glyph bitmaps. Read by the rasterizer, not
    /// by the CPU — on real hardware the chip is wired to the video circuitry only.
    /// </summary>
    public const string CHARGEN_ROM_NAME = "chargen";

    /// <summary>
    /// The Disk II controller card's 256-byte P5 boot ROM (16-sector, 341-0027), mapped at
    /// $C600 while a disk is inserted. Optional, like the card it belongs to: without it the
    /// machine is simply a diskless Apple II Plus, and only booting a disk image is unavailable.
    /// </summary>
    public const string DISK2_ROM_NAME = "disk2";

    public static readonly List<string> RequiredROMs = new() { SYSTEM_ROM_NAME, CHARGEN_ROM_NAME };

    /// <summary>ROMs the machine can use but does not need in order to run.</summary>
    public static readonly List<string> OptionalROMs = new() { DISK2_ROM_NAME };

    /// <summary>
    /// Where to obtain the ROMs. Unlike zimmers.net for Commodore machines there is no equally
    /// canonical Apple II ROM host; the Asimov mirror is the most actively maintained archive.
    /// </summary>
    public const string ROM_SOURCE_INFO_URL = "https://mirrors.apple2.org.za/ftp.apple.asimov.net/emulators/rom_images/";

    /// <summary>The 12 KB $D000-$FFFF image, published as a bare file.</summary>
    public static string DEFAULT_SYSTEM_ROM_DOWNLOAD_URL = $"{ROM_SOURCE_INFO_URL}apple.rom";

    /// <summary>
    /// The character generator is only published inside a multi-file archive, so it has to be
    /// extracted by entry name — the archive holds 42 <c>.bin</c> files, so matching on extension
    /// alone would be ambiguous.
    /// </summary>
    public static string DEFAULT_CHARGEN_ROM_DOWNLOAD_URL = $"{ROM_SOURCE_INFO_URL}ROMS.ZIP";
    public const string DEFAULT_CHARGEN_ROM_ZIP_ENTRY = "3410036.BIN";

    /// <summary>
    /// The Disk II 16-sector boot ROM. The archive's plainly-named 341-0027 file is a raw PROM
    /// dump with the D4-D7 data lines in hardware order; this variant has them in CPU bit order
    /// (its first bytes are the A2 20 A0 00 A2 03 signature the Autostart slot scan checks for).
    /// </summary>
    public static string DEFAULT_DISK2_ROM_DOWNLOAD_URL =
        $"{ROM_SOURCE_INFO_URL}Apple%20Disk%20II%2016%20Sector%20Interface%20Card%20ROM%20P5%20-%20341-0027.bin-with-D4-D7%20data%20bits%20swapped.bin";

    /// <summary>
    /// SHA1 checksums of accepted Apple II Plus system ROM images (version label → sha1 hex).
    /// Both the trimmed 12 KB $D000-$FFFF image and the 20 KB $B000-$FFFF layout that older
    /// emulator distributions use are accepted; see <c>Apple2.ExtractSystemRomImage</c>.
    /// </summary>
    public static Dictionary<string, string> DefaultSystemROMChecksums = new()
    {
        { "Apple II Plus (Applesoft + Autostart), 12 KB $D000-$FFFF", "8c5ca0c39005dfb0898af2c0992f797cc77530c0" },
        { "Apple II Plus (Applesoft + Autostart), 20 KB $B000-$FFFF", "29a53f3bb158b160433369e8e4a1d7cd5bf68ac6" },
    };

    /// <summary>
    /// SHA1 checksums of accepted character generator images. The 2 KB dump of the Apple II
    /// Plus part (341-0036) is the common circulating form; only its leading 512 bytes carry
    /// unique data.
    /// </summary>
    public static Dictionary<string, string> DefaultChargenROMChecksums = new()
    {
        { "341-0036 (Apple II Plus character generator), 2 KB", "f9d312f128c9557d9d6ac03bfad6c3ddf83e5659" },
    };

    /// <summary>SHA1 checksums of accepted Disk II boot ROM images (CPU bit order).</summary>
    public static Dictionary<string, string> DefaultDisk2ROMChecksums = new()
    {
        { "341-0027 (Disk II 16-sector boot ROM P5), 256 bytes", "d4181c9f046aafc3fb326b381baac809d9e38d16" },
    };

    private bool _isDirty = false;
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

    /// <summary>
    /// On by default, as the C64's is (<c>C64SystemConfig</c> sets the same default in its
    /// constructor). A machine that boots silently reads as a machine whose sound is not
    /// implemented, and the speaker is part of how an Apple II sounds — the boot beep included.
    /// Turn it off in the configuration dialog.
    ///
    /// <para>The default has to live here rather than in a host's <c>appsettings.json</c>, because
    /// the Browser app builds its configuration from browser local storage alone and never reads
    /// an appsettings file — so on a first visit only this default applies.</para>
    /// </summary>
    public bool AudioEnabled { get; set; } = true;

    private Apple2MonitorColor _monitorColor = Apple2MonitorColor.Color;
    public Apple2MonitorColor MonitorColor
    {
        get => _monitorColor;
        set { _monitorColor = value; _isDirty = true; }
    }

    /// <summary>
    /// Whether host keys drive the game port. Lives here, on the persisted system config, rather
    /// than on the runtime mapping — the same place the C64 keeps it, so there is one source of
    /// truth that survives a restart and one place the config dialog and the sidebar both bind to.
    /// </summary>
    private bool _keyboardJoystickEnabled;
    public bool KeyboardJoystickEnabled
    {
        get => _keyboardJoystickEnabled;
        set { _keyboardJoystickEnabled = value; _isDirty = true; }
    }

    private CpuCompatibilityProfile _cpuCompatibilityProfile = CpuCompatibilityProfile.StableUnofficial;
    public CpuCompatibilityProfile CpuCompatibilityProfile
    {
        get => _cpuCompatibilityProfile;
        set { _cpuCompatibilityProfile = value; _isDirty = true; }
    }

    private string _romDirectory = string.Empty;
    public string ROMDirectory
    {
        get => _romDirectory;
        set { _romDirectory = value ?? string.Empty; _isDirty = true; }
    }

    [JsonIgnore]
    public static string DefaultROMDirectory => AppStoragePaths.GetRomDirectory(Apple2System.SystemName);

    [JsonIgnore]
    public string EffectiveROMDirectory => string.IsNullOrWhiteSpace(ROMDirectory)
        && !OperatingSystem.IsBrowser()
        ? DefaultROMDirectory
        : ROMDirectory;

    /// <summary>
    /// Where each ROM can be downloaded from. Canonical; <see cref="ROMDownloadUrls"/> is a
    /// projection of it. Both URLs and the resulting checksums are verified against the archive.
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, RomDownloadSource> ROMDownloadSources { get; } = new()
    {
        { SYSTEM_ROM_NAME, new RomDownloadSource(DEFAULT_SYSTEM_ROM_DOWNLOAD_URL) },
        {
            CHARGEN_ROM_NAME,
            new RomDownloadSource(DEFAULT_CHARGEN_ROM_DOWNLOAD_URL, ZipEntryName: DEFAULT_CHARGEN_ROM_ZIP_ENTRY)
        },
        { DISK2_ROM_NAME, new RomDownloadSource(DEFAULT_DISK2_ROM_DOWNLOAD_URL) },
    };

    /// <summary>
    /// URL-only view of <see cref="ROMDownloadSources"/>. Note that it cannot express the
    /// character generator's ZIP entry — a host using this instead of <see cref="RomDownloader"/>
    /// would download the archive itself, not the ROM.
    /// </summary>
    public Dictionary<string, string> ROMDownloadUrls
        => ROMDownloadSources.ToDictionary(entry => entry.Key, entry => entry.Value.Url);

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
        if (rom.Name == SYSTEM_ROM_NAME)
            rom.ValidVersionChecksums = new Dictionary<string, string>(DefaultSystemROMChecksums);
        else if (rom.Name == CHARGEN_ROM_NAME)
            rom.ValidVersionChecksums = new Dictionary<string, string>(DefaultChargenROMChecksums);
        else if (rom.Name == DISK2_ROM_NAME)
            rom.ValidVersionChecksums = new Dictionary<string, string>(DefaultDisk2ROMChecksums);
    }

    public bool HasROM(string romName) => _roms.Any(x => x.Name == romName);
    public ROM GetROM(string romName) => _roms.Single(x => x.Name == romName);

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
            var rom = new ROM
            {
                Name = romName,
                File = file,
                Data = data
            };

            ApplyDefaultChecksums(rom);
            ROMs.Add(rom);
        }

        _isDirty = true;
    }

    [JsonIgnore]
    public Type? AudioProviderType { get; private set; }

    /// <summary>Serializable form of <see cref="AudioProviderType"/>.</summary>
    [JsonPropertyName("AudioProviderType")]
    public string? AudioProviderTypeName
    {
        get => ConfiguredTypeName.Format(AudioProviderType);
        set => SetAudioProviderType(ConfiguredTypeName.Resolve(value));
    }

    [JsonIgnore]
    public Type? AudioTargetType { get; private set; }

    /// <summary>Serializable form of <see cref="AudioTargetType"/>.</summary>
    [JsonPropertyName("AudioTargetType")]
    public string? AudioTargetTypeName
    {
        get => ConfiguredTypeName.Format(AudioTargetType);
        set => SetAudioTargetType(ConfiguredTypeName.Resolve(value));
    }

    public List<Type> GetSupportedRenderProviderTypes() =>
        new() { typeof(Apple2Rasterizer), typeof(Apple2VideoCommandStream) };

    /// <summary>
    /// Only the sample path. The machine emits no note or voice information for a synth-command
    /// stream to describe — its whole output is a one-bit cone position over time.
    /// </summary>
    public List<Type> GetSupportedAudioProviderTypes() =>
        new() { typeof(Apple2SpeakerSampleProvider) };

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
        if (audioProviderType == null)
        {
            AudioProviderType = null;
            _isDirty = true;
            return;
        }

        if (!GetSupportedAudioProviderTypes().Contains(audioProviderType))
            throw new DotNet6502Exception($"AudioProvider type {audioProviderType.FullName} is not supported.");
        AudioProviderType = audioProviderType;
        _isDirty = true;
    }

    public void SetAudioTargetType(Type? audioTargetType)
    {
        AudioTargetType = audioTargetType;
        _isDirty = true;
    }

    public Apple2SystemConfig()
    {
        _romDirectory = string.Empty;
        SetRenderProviderType(GetSupportedRenderProviderTypes().First());
    }

    public void ClearDirty() => _isDirty = false;

    public object Clone()
    {
        var clone = (Apple2SystemConfig)MemberwiseClone();
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

        var effectiveRomDirectory = EffectiveROMDirectory;
        var romDir = PathHelper.ExpandOSEnvironmentVariables(effectiveRomDirectory);
        if (_roms.Any(rom => !string.IsNullOrEmpty(rom.File)) && !string.IsNullOrEmpty(romDir) && !Directory.Exists(romDir))
            validationErrors.Add($"{nameof(ROMDirectory)} does not exist: {romDir}");

        if (validationErrors.Count == 0)
        {
            foreach (var rom in _roms)
            {
                if (!rom.Validate(out var romErrors, effectiveRomDirectory))
                    validationErrors.AddRange(romErrors);
            }
        }

        return validationErrors.Count == 0;
    }
}
