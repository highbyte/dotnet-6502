using System.Security.Cryptography;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

/// <summary>
/// Locates, and optionally fetches, the KERNAL, BASIC and character ROMs the C64 program-level
/// integration tests need. The ROMs are copyrighted and are not checked in; tests that need them
/// skip visibly (see <see cref="RequiresC64RomsFactAttribute"/>) rather than pass without running,
/// following the Apple II precedent in <c>Apple2TestRoms</c>.
///
/// Resolution order:
/// <list type="number">
/// <item><c>DOTNET6502_C64_ROM_DIR</c>, else the app's own C64 ROM directory
/// (<see cref="C64SystemConfig.DefaultROMDirectory"/>), using the file names the app's ROM download
/// writes.</item>
/// <item>When <c>DOTNET6502_DOWNLOAD_TEST_ROMS</c> is <c>1</c>/<c>true</c>, missing or corrupt files
/// are downloaded from the same public sources the app uses and written to that directory. CI sets
/// this so the C64 tests have an oracle; developers opt in explicitly.</item>
/// </list>
/// Every file is verified against the SHA-1 checksums the C64 configuration already carries, so a
/// wrong or truncated download is reported, not booted.
/// </summary>
internal static class C64TestRoms
{
    public const string RomDirectoryEnvironmentVariable = "DOTNET6502_C64_ROM_DIR";
    public const string DownloadEnvironmentVariable = "DOTNET6502_DOWNLOAD_TEST_ROMS";

    private sealed record RomSpec(string Name, RomDownloadSource Source, IReadOnlyDictionary<string, string> Checksums)
    {
        public string FileName => Source.ResolveFileName();
    }

    private static readonly RomSpec[] s_roms =
    [
        new(C64SystemConfig.KERNAL_ROM_NAME, new RomDownloadSource(C64SystemConfig.DEFAULT_KERNAL_ROM_DOWNLOAD_URL), C64SystemConfig.DefaultKernalROMChecksums),
        new(C64SystemConfig.BASIC_ROM_NAME, new RomDownloadSource(C64SystemConfig.DEFAULT_BASIC_ROM_DOWNLOAD_URL), C64SystemConfig.DefaultBasicROMChecksums),
        new(C64SystemConfig.CHARGEN_ROM_NAME, new RomDownloadSource(C64SystemConfig.DEFAULT_CHARGEN_ROM_DOWNLOAD_URL), C64SystemConfig.DefaultCharGenROMChecksums),
    ];

    private static readonly Lazy<string?> s_missingReason = new(Resolve, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string RomDirectory
    {
        get
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(RomDirectoryEnvironmentVariable);
            return string.IsNullOrWhiteSpace(fromEnvironment) ? C64SystemConfig.DefaultROMDirectory : fromEnvironment;
        }
    }

    public static bool DownloadEnabled
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(DownloadEnvironmentVariable)?.Trim();
            return value is not null && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Null when every ROM is present and verified; otherwise the skip reason.</summary>
    public static string? MissingReason => s_missingReason.Value;

    /// <summary>The ROM list a <see cref="C64Config"/> needs to load the verified files.</summary>
    public static List<ROM> CreateRomList()
        => s_roms.Select(rom => new ROM
        {
            Name = rom.Name,
            File = rom.FileName,
            ValidVersionChecksums = new Dictionary<string, string>(rom.Checksums),
        }).ToList();

    /// <summary>A PAL C64 configuration that boots the real ROMs from <see cref="RomDirectory"/>.</summary>
    public static C64Config CreateConfig(string c64Model = "C64PAL", string vic2Model = "PAL")
        => new()
        {
            LoadROMs = true,
            ROMDirectory = RomDirectory,
            ROMs = CreateRomList(),
            C64Model = c64Model,
            Vic2Model = vic2Model,
        };

    private static string? Resolve()
    {
        var directory = RomDirectory;
        var missing = new List<string>();

        foreach (var rom in s_roms)
        {
            var path = Path.Combine(directory, rom.FileName);
            if (IsVerified(path, rom))
                continue;

            if (DownloadEnabled)
            {
                try
                {
                    Download(rom, path);
                }
                catch (Exception ex)
                {
                    return $"Download of C64 {rom.Name} ROM from {rom.Source.Url} failed: {ex.Message}";
                }

                if (IsVerified(path, rom))
                    continue;
                return $"Downloaded C64 {rom.Name} ROM ({path}) does not match any known checksum.";
            }

            missing.Add(rom.FileName);
        }

        if (missing.Count == 0)
            return null;

        return $"No verified C64 ROMs found in {directory} (missing or wrong checksum: {string.Join(", ", missing)}). " +
               $"Set {RomDirectoryEnvironmentVariable} to a directory holding them, or set {DownloadEnvironmentVariable}=1 to fetch them from the public archive.";
    }

    private static bool IsVerified(string path, RomSpec rom)
    {
        if (!File.Exists(path))
            return false;
        var checksum = Convert.ToHexStringLower(SHA1.HashData(File.ReadAllBytes(path)));
        return rom.Checksums.Values.Contains(checksum, StringComparer.OrdinalIgnoreCase);
    }

    private static void Download(RomSpec rom, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var httpClient = new HttpClient();
        var downloader = new RomDownloader(NullLoggerFactory.Instance, httpClient);
        var bytes = downloader.DownloadRomAsync(rom.Name, rom.Source).GetAwaiter().GetResult();
        File.WriteAllBytes(path, bytes);
    }
}

/// <summary>A test needing the genuine C64 KERNAL, BASIC and character ROMs. Skips, visibly, when they are absent.</summary>
public sealed class RequiresC64RomsFactAttribute : FactAttribute
{
    public RequiresC64RomsFactAttribute()
    {
        if (C64TestRoms.MissingReason is { } reason)
            Skip = reason;
    }
}
