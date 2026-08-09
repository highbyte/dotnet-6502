using Highbyte.DotNet6502.Systems.Apple2.Config;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Locates the copyrighted files the Apple II integration tests need. None of them can be checked
/// in, so those tests are opt-in — but they must announce that they did not run.
///
/// A test that quietly returns early reports as <em>passed</em>, which is how
/// <c>Injected_Basic_Program_Runs_And_Lists</c> shipped broken: CI has no ROMs, so it was green
/// from the day it was written without ever executing a line, and the bad assertion only surfaced
/// once a ROM happened to be present locally. The attributes below set <see cref="FactAttribute.Skip"/>
/// at discovery instead, so a missing ROM reads as "skipped, here is why" rather than as coverage.
/// </summary>
internal static class Apple2TestRoms
{
    public const string SystemRomPathEnvironmentVariable = "DOTNET6502_APPLE2_ROM";
    public const string CharacterRomPathEnvironmentVariable = "DOTNET6502_APPLE2_CHARGEN_ROM";
    public const string Disk2RomPathEnvironmentVariable = "DOTNET6502_APPLE2_DISK2_ROM";
    public const string BootDskPathEnvironmentVariable = "DOTNET6502_APPLE2_BOOT_DSK";

    /// <summary>
    /// A bootable, DOS-sector-ordered ProDOS disk image. Kept separate from the DOS 3.3 boot disk
    /// because it tests a different thing: that the machine has the 64 KB ProDOS requires.
    /// </summary>
    public const string ProdosDskPathEnvironmentVariable = "DOTNET6502_APPLE2_PRODOS_DSK";

    /// <summary>
    /// File names as published by the ROM archive — the same names the app's ROM download writes
    /// into the ROM directory. The system ROM is published both as the bare 12 KB image and in the
    /// 20 KB layout, and the loader accepts either.
    /// </summary>
    public static readonly string[] SystemRomFileNames = ["apple.rom", "APPLE2_.ROM"];

    public const string CharacterRomFileName = "3410036.BIN";

    public static readonly string[] Disk2RomFileNames =
        ["Apple Disk II 16 Sector Interface Card ROM P5 - 341-0027.bin-with-D4-D7 data bits swapped.bin"];

    public static string? ResolveSystemRomPath()
        => Resolve(SystemRomPathEnvironmentVariable, SystemRomFileNames);

    public static string? ResolveCharacterRomPath()
        => Resolve(CharacterRomPathEnvironmentVariable, [CharacterRomFileName]);

    public static string? ResolveDisk2RomPath()
        => Resolve(Disk2RomPathEnvironmentVariable, Disk2RomFileNames);

    /// <summary>
    /// Disk images have no default location on purpose: they are user content kept wherever the
    /// user likes (the host picks them with a file dialog), unlike ROMs which the config manages
    /// in a known directory.
    /// </summary>
    public static string? ResolveBootDskPath()
        => ResolveDiskImagePath(BootDskPathEnvironmentVariable);

    public static string? ResolveProdosDskPath()
        => ResolveDiskImagePath(ProdosDskPathEnvironmentVariable);

    private static string? ResolveDiskImagePath(string environmentVariable)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(environmentVariable);
        return !string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment)
            ? fromEnvironment
            : null;
    }

    private static string? Resolve(string environmentVariable, IEnumerable<string> fileNames)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
            return fromEnvironment;

        return fileNames
            .Select(name => Path.Combine(Apple2SystemConfig.DefaultROMDirectory, name))
            .FirstOrDefault(File.Exists);
    }

    public static string MissingFileReason(string what, string environmentVariable, IEnumerable<string> fileNames)
        => $"No {what} found. Set {environmentVariable}, or place one of " +
           $"[{string.Join(", ", fileNames)}] in {Apple2SystemConfig.DefaultROMDirectory}.";
}

/// <summary>A test needing the genuine Apple II system ROM. Skips, visibly, when it is absent.</summary>
public sealed class RequiresApple2RomFactAttribute : FactAttribute
{
    public RequiresApple2RomFactAttribute()
    {
        if (Apple2TestRoms.ResolveSystemRomPath() == null)
            Skip = Apple2TestRoms.MissingFileReason(
                "Apple II system ROM",
                Apple2TestRoms.SystemRomPathEnvironmentVariable,
                Apple2TestRoms.SystemRomFileNames);
    }
}

/// <summary>The <see cref="TheoryAttribute"/> counterpart of <see cref="RequiresApple2RomFactAttribute"/>.</summary>
public sealed class RequiresApple2RomTheoryAttribute : TheoryAttribute
{
    public RequiresApple2RomTheoryAttribute()
    {
        if (Apple2TestRoms.ResolveSystemRomPath() == null)
            Skip = Apple2TestRoms.MissingFileReason(
                "Apple II system ROM",
                Apple2TestRoms.SystemRomPathEnvironmentVariable,
                Apple2TestRoms.SystemRomFileNames);
    }
}

/// <summary>A test needing the system ROM <em>and</em> the character generator ROM.</summary>
public sealed class RequiresApple2RomAndCharacterRomFactAttribute : FactAttribute
{
    public RequiresApple2RomAndCharacterRomFactAttribute()
    {
        if (Apple2TestRoms.ResolveSystemRomPath() == null)
            Skip = Apple2TestRoms.MissingFileReason(
                "Apple II system ROM",
                Apple2TestRoms.SystemRomPathEnvironmentVariable,
                Apple2TestRoms.SystemRomFileNames);
        else if (Apple2TestRoms.ResolveCharacterRomPath() == null)
            Skip = Apple2TestRoms.MissingFileReason(
                "Apple II character generator ROM",
                Apple2TestRoms.CharacterRomPathEnvironmentVariable,
                [Apple2TestRoms.CharacterRomFileName]);
    }
}

/// <summary>
/// A test that boots a real disk: needs the system ROM, the Disk II boot ROM, and a bootable
/// DOS 3.3 image.
/// </summary>
public sealed class RequiresApple2Disk2BootFactAttribute : FactAttribute
{
    public RequiresApple2Disk2BootFactAttribute()
    {
        if (Apple2TestRoms.ResolveSystemRomPath() == null)
            Skip = Apple2TestRoms.MissingFileReason(
                "Apple II system ROM",
                Apple2TestRoms.SystemRomPathEnvironmentVariable,
                Apple2TestRoms.SystemRomFileNames);
        else if (Apple2TestRoms.ResolveDisk2RomPath() == null)
            Skip = Apple2TestRoms.MissingFileReason(
                "Disk II boot ROM",
                Apple2TestRoms.Disk2RomPathEnvironmentVariable,
                Apple2TestRoms.Disk2RomFileNames);
        else if (Apple2TestRoms.ResolveBootDskPath() == null)
            Skip = $"No bootable DOS 3.3 disk image. Set {Apple2TestRoms.BootDskPathEnvironmentVariable} " +
                   "to a .dsk path (there is deliberately no default location for disk images).";
    }
}

/// <summary>
/// A test needing the genuine ROMs plus a bootable, DOS-ordered ProDOS disk image. Skips, visibly,
/// when any of them is absent — ProDOS images are user content with no default location, exactly
/// like the DOS 3.3 boot disk.
/// </summary>
public sealed class RequiresApple2ProdosBootFactAttribute : FactAttribute
{
    public RequiresApple2ProdosBootFactAttribute()
    {
        if (Apple2TestRoms.ResolveSystemRomPath() == null)
            Skip = Apple2TestRoms.MissingFileReason(
                "Apple II system ROM",
                Apple2TestRoms.SystemRomPathEnvironmentVariable,
                Apple2TestRoms.SystemRomFileNames);
        else if (Apple2TestRoms.ResolveDisk2RomPath() == null)
            Skip = Apple2TestRoms.MissingFileReason(
                "Disk II boot ROM",
                Apple2TestRoms.Disk2RomPathEnvironmentVariable,
                Apple2TestRoms.Disk2RomFileNames);
        else if (Apple2TestRoms.ResolveProdosDskPath() == null)
            Skip = $"No bootable ProDOS disk image. Set {Apple2TestRoms.ProdosDskPathEnvironmentVariable} " +
                   "to a DOS-sector-ordered .dsk path.";
    }
}
