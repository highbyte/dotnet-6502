namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge.Crt;

public static class C64CrtCartridgeFactory
{
    public static IC64Cartridge Create(C64CrtImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        return image.Header.HardwareType switch
        {
            (ushort)C64CrtHardwareType.Generic => CreateGeneric(image),
            (ushort)C64CrtHardwareType.ActionReplay => CreateActionReplay(image),
            (ushort)C64CrtHardwareType.FinalCartridgeIII => CreateFinalCartridgeIII(image),
            (ushort)C64CrtHardwareType.Ocean => CreateOcean(image),
            (ushort)C64CrtHardwareType.Expert => CreateExpert(image),
            (ushort)C64CrtHardwareType.EpyxFastLoad => CreateEpyxFastLoad(image),
            (ushort)C64CrtHardwareType.MagicDesk => CreateMagicDesk(image),
            _ => throw new C64UnsupportedCrtHardwareException(image.Header.HardwareType),
        };
    }

    private static IC64Cartridge CreateGeneric(C64CrtImage image)
    {
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

    private static IC64Cartridge CreateMagicDesk(C64CrtImage image)
    {
        if (image.Chips.Any(chip => chip.Type != C64CrtChipType.Rom))
            throw new C64CrtImageException("Magic Desk CRT images support ROM CHIP packets only.");

        var banks = new List<KeyValuePair<ushort, byte[]>>(image.Chips.Count);
        var bankNumbers = new HashSet<ushort>();
        foreach (var chip in image.Chips)
        {
            if (chip.Bank > 127)
                throw new C64CrtImageException($"Magic Desk CRT bank {chip.Bank} exceeds the supported maximum bank 127.");
            if (chip.LoadAddress is not (0x8000 or 0xA000))
                throw new C64CrtImageException($"Magic Desk CRT bank {chip.Bank} must load at 0x8000 or 0xA000.");
            if (chip.Data.Length != C64BankedRom.BankSize)
                throw new C64CrtImageException($"Magic Desk CRT bank {chip.Bank} must contain exactly 8K of ROM data.");
            if (!bankNumbers.Add(chip.Bank))
                throw new C64CrtImageException($"Magic Desk CRT bank {chip.Bank} is duplicated.");

            banks.Add(new KeyValuePair<ushort, byte[]>(chip.Bank, chip.Data));
        }

        var rom = new C64BankedRom(banks, maximumBank: 127);
        return new C64MagicDeskCartridge(
            rom,
            string.IsNullOrWhiteSpace(image.Header.Name) ? "Magic Desk" : image.Header.Name);
    }

    private static IC64Cartridge CreateActionReplay(C64CrtImage image)
    {
        if (image.Chips.Any(chip => chip.Type != C64CrtChipType.Rom))
            throw new C64CrtImageException("Action Replay CRT images support ROM CHIP packets only.");

        var banks = BuildBankedRomChips(image, "Action Replay", maximumBank: 3);
        if (banks.Count != C64ActionReplayCartridge.RomBankCount)
            throw new C64CrtImageException("Action Replay CRT images must contain exactly four ROM banks.");
        for (ushort bank = 0; bank < C64ActionReplayCartridge.RomBankCount; bank++)
        {
            if (!banks.ContainsKey(bank))
                throw new C64CrtImageException($"Action Replay CRT bank {bank} is missing.");
        }

        var rom = new C64BankedRom(banks, maximumBank: 3);
        return new C64ActionReplayCartridge(
            rom,
            string.IsNullOrWhiteSpace(image.Header.Name) ? "Action Replay" : image.Header.Name);
    }

    private static IC64Cartridge CreateFinalCartridgeIII(C64CrtImage image)
    {
        if (image.Chips.Any(chip => chip.Type != C64CrtChipType.Rom))
            throw new C64CrtImageException("Final Cartridge III CRT images support ROM CHIP packets only.");
        if (image.Chips.Count is not (
            C64FinalCartridgeIIICartridge.StandardRomBankCount or
            C64FinalCartridgeIIICartridge.ExtendedRomBankCount))
        {
            throw new C64CrtImageException("Final Cartridge III CRT images must contain exactly 4 or 16 ROM banks.");
        }

        var bankCount = image.Chips.Count;
        var romlBanks = new Dictionary<ushort, byte[]>(bankCount);
        var romhBanks = new Dictionary<ushort, byte[]>(bankCount);
        foreach (var chip in image.Chips)
        {
            if (chip.Bank >= bankCount)
                throw new C64CrtImageException($"Final Cartridge III CRT bank {chip.Bank} exceeds maximum bank {bankCount - 1}.");
            if (chip.LoadAddress != 0x8000)
                throw new C64CrtImageException($"Final Cartridge III CRT bank {chip.Bank} must load at 0x8000.");
            if (chip.Data.Length != C64FinalCartridgeIIICartridge.CrtBankSize)
                throw new C64CrtImageException($"Final Cartridge III CRT bank {chip.Bank} must contain exactly 16K of ROM data.");
            if (!romlBanks.TryAdd(chip.Bank, chip.Data[..C64BankedRom.BankSize]))
                throw new C64CrtImageException($"Final Cartridge III CRT bank {chip.Bank} is duplicated.");

            romhBanks.Add(chip.Bank, chip.Data[C64BankedRom.BankSize..]);
        }

        for (ushort bank = 0; bank < bankCount; bank++)
        {
            if (!romlBanks.ContainsKey(bank))
                throw new C64CrtImageException($"Final Cartridge III CRT bank {bank} is missing.");
        }

        return new C64FinalCartridgeIIICartridge(
            new C64BankedRom(romlBanks, (ushort)(bankCount - 1)),
            new C64BankedRom(romhBanks, (ushort)(bankCount - 1)),
            bankCount,
            string.IsNullOrWhiteSpace(image.Header.Name)
                ? "The Final Cartridge III"
                : image.Header.Name);
    }

    private static IC64Cartridge CreateOcean(C64CrtImage image)
    {
        if (image.Chips.Any(chip => chip.Type != C64CrtChipType.Rom))
            throw new C64CrtImageException("Ocean CRT images support ROM CHIP packets only.");

        var banks = BuildBankedRomChips(image, "Ocean", maximumBank: 63);
        var bankCount = banks.Count;
        if (!IsPowerOfTwo(bankCount))
            throw new C64CrtImageException("Ocean CRT bank count must be a power of two.");
        for (ushort bank = 0; bank < bankCount; bank++)
        {
            if (!banks.ContainsKey(bank))
                throw new C64CrtImageException($"Ocean CRT bank {bank} is missing.");
        }

        var rom = new C64BankedRom(banks, maximumBank: 63);
        return new C64OceanCartridge(
            rom,
            useEightKMode: bankCount == 64,
            name: string.IsNullOrWhiteSpace(image.Header.Name) ? "Ocean" : image.Header.Name);
    }

    private static IC64Cartridge CreateExpert(C64CrtImage image)
    {
        if (image.Chips.Count != 1)
            throw new C64CrtImageException("Expert CRT images must contain exactly one CHIP packet.");

        var chip = image.Chips[0];
        if (chip.Bank != 0)
            throw new C64CrtImageException("Expert CRT images must use bank 0.");
        if (chip.LoadAddress != 0x8000)
            throw new C64CrtImageException("Expert CRT RAM must load at 0x8000.");
        if (chip.Data.Length != C64ExpertCartridge.RamSize)
            throw new C64CrtImageException("Expert CRT RAM must contain exactly 8K of data.");

        return new C64ExpertCartridge(
            chip.Data,
            string.IsNullOrWhiteSpace(image.Header.Name) ? "Expert Cartridge" : image.Header.Name);
    }

    private static IC64Cartridge CreateEpyxFastLoad(C64CrtImage image)
    {
        if (image.Chips.Count != 1)
            throw new C64CrtImageException("Epyx FastLoad CRT images must contain exactly one CHIP packet.");

        var chip = image.Chips[0];
        if (chip.Type != C64CrtChipType.Rom)
            throw new C64CrtImageException("Epyx FastLoad CRT images support ROM CHIP packets only.");
        if (chip.Bank != 0)
            throw new C64CrtImageException("Epyx FastLoad CRT images must use bank 0.");
        if (chip.LoadAddress != 0x8000)
            throw new C64CrtImageException("Epyx FastLoad CRT ROM must load at 0x8000.");
        if (chip.Data.Length != C64EpyxFastLoadCartridge.RomSize)
            throw new C64CrtImageException("Epyx FastLoad CRT ROM must contain exactly 8K of data.");

        return new C64EpyxFastLoadCartridge(
            chip.Data,
            string.IsNullOrWhiteSpace(image.Header.Name) ? "Epyx FastLoad" : image.Header.Name);
    }

    private static Dictionary<ushort, byte[]> BuildBankedRomChips(
        C64CrtImage image,
        string hardwareName,
        ushort maximumBank)
    {
        var banks = new Dictionary<ushort, byte[]>(image.Chips.Count);
        foreach (var chip in image.Chips)
        {
            if (chip.Bank > maximumBank)
                throw new C64CrtImageException($"{hardwareName} CRT bank {chip.Bank} exceeds the supported maximum bank {maximumBank}.");
            if (chip.LoadAddress is not (0x8000 or 0xA000))
                throw new C64CrtImageException($"{hardwareName} CRT bank {chip.Bank} must load at 0x8000 or 0xA000.");
            if (chip.Data.Length != C64BankedRom.BankSize)
                throw new C64CrtImageException($"{hardwareName} CRT bank {chip.Bank} must contain exactly 8K of ROM data.");
            if (!banks.TryAdd(chip.Bank, chip.Data))
                throw new C64CrtImageException($"{hardwareName} CRT bank {chip.Bank} is duplicated.");
        }
        return banks;
    }

    private static bool IsPowerOfTwo(int value)
        => value > 0 && (value & (value - 1)) == 0;

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
