namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge.Crt;

public static class C64CrtCartridgeFactory
{
    public static IC64Cartridge Create(C64CrtImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.Header.HardwareType != (ushort)C64CrtHardwareType.Generic)
            throw new C64UnsupportedCrtHardwareException(image.Header.HardwareType);

        if (image.Chips.Any(chip => chip.Type != C64CrtChipType.Rom))
            throw new C64CrtImageException("Generic CRT images currently support ROM CHIP packets only.");
        if (image.Chips.Any(chip => chip.Bank != 0))
            throw new C64CrtImageException("Generic CRT images currently support bank 0 only.");

        var lines = new C64CartridgeLines(image.Header.GameHigh, image.Header.ExromHigh);
        ValidateChipRanges(lines, image.Chips);
        var roml = BuildWindow(image.Chips, 0x8000);
        var romhBaseAddress = GetRomhBaseAddress(lines);
        var romh = romhBaseAddress.HasValue
            ? BuildWindow(image.Chips, romhBaseAddress.Value)
            : null;

        ValidateGenericShape(lines, roml, romh);

        return new C64RomCartridge(
            roml,
            romh,
            lines,
            string.IsNullOrWhiteSpace(image.Header.Name) ? "CRT cartridge" : image.Header.Name);
    }

    private static byte[]? BuildWindow(IReadOnlyList<C64CrtChip> chips, ushort baseAddress)
    {
        var windowStart = (int)baseAddress;
        var windowEnd = windowStart + C64RomCartridge.RomWindowSize;
        var matching = chips.Where(chip =>
        {
            var chipStart = (int)chip.LoadAddress;
            var chipEnd = chipStart + chip.Data.Length;
            return chipStart < windowEnd && chipEnd > windowStart;
        }).ToArray();
        if (matching.Length == 0)
            return null;

        var window = new byte[C64RomCartridge.RomWindowSize];
        var written = new bool[C64RomCartridge.RomWindowSize];
        foreach (var chip in matching)
        {
            var chipStart = (int)chip.LoadAddress;
            var copyStart = Math.Max(chipStart, windowStart);
            var copyEnd = Math.Min(chipStart + chip.Data.Length, windowEnd);
            for (var address = copyStart; address < copyEnd; address++)
            {
                var target = address - windowStart;
                if (written[target])
                    throw new C64CrtImageException($"Generic CRT CHIP packets overlap at address 0x{address:X4}.");
                written[target] = true;
                window[target] = chip.Data[address - chipStart];
            }
        }

        if (written.Any(value => !value))
            throw new C64CrtImageException($"Generic CRT ROM window at 0x{baseAddress:X4} must contain exactly 8K of ROM data.");

        return window;
    }

    private static void ValidateChipRanges(
        C64CartridgeLines lines,
        IReadOnlyList<C64CrtChip> chips)
    {
        (int Start, int End)[] allowedRanges = lines switch
        {
            { GameHigh: true, ExromHigh: false } => [(0x8000, 0xA000)],
            { GameHigh: false, ExromHigh: false } => [(0x8000, 0xC000)],
            { GameHigh: false, ExromHigh: true } => [(0x8000, 0xA000), (0xE000, 0x10000)],
            _ => throw new C64CrtImageException("Generic CRT uses an unsupported GAME/EXROM line combination."),
        };

        foreach (var chip in chips)
        {
            var chipStart = (int)chip.LoadAddress;
            var chipEnd = chipStart + chip.Data.Length;
            var coveredUntil = chipStart;

            foreach (var range in allowedRanges)
            {
                if (coveredUntil < range.Start)
                    break;
                if (coveredUntil >= range.End)
                    continue;

                coveredUntil = Math.Min(chipEnd, range.End);
                if (coveredUntil == chipEnd)
                    break;
            }

            if (coveredUntil != chipEnd)
            {
                throw new C64CrtImageException(
                    $"Generic CRT CHIP at 0x{chip.LoadAddress:X4} contains data outside the cartridge ROM windows selected by GAME/EXROM.");
            }
        }
    }

    private static ushort? GetRomhBaseAddress(C64CartridgeLines lines)
    {
        if (lines == new C64CartridgeLines(GameHigh: false, ExromHigh: false))
            return 0xA000;
        if (lines == new C64CartridgeLines(GameHigh: false, ExromHigh: true))
            return 0xE000;
        return null;
    }

    private static void ValidateGenericShape(C64CartridgeLines lines, byte[]? roml, byte[]? romh)
    {
        if (lines == new C64CartridgeLines(GameHigh: true, ExromHigh: false))
        {
            if (roml == null || romh != null)
                throw new C64CrtImageException("Generic 8K CRT must contain one complete ROML window at 0x8000.");
            return;
        }

        if (lines == new C64CartridgeLines(GameHigh: false, ExromHigh: false))
        {
            if (roml == null || romh == null)
                throw new C64CrtImageException("Generic 16K CRT must contain complete ROML and ROMH windows at 0x8000 and 0xA000.");
            return;
        }

        if (lines == new C64CartridgeLines(GameHigh: false, ExromHigh: true))
        {
            if (romh == null)
                throw new C64CrtImageException("Generic Ultimax CRT must contain a complete ROMH window at 0xE000.");
            return;
        }

        throw new C64CrtImageException("Generic CRT uses an unsupported GAME/EXROM line combination.");
    }
}
