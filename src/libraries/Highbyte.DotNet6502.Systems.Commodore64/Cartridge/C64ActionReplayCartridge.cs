namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// Action Replay 4.2/5/6 cartridge with four 8K ROM banks, 8K RAM,
/// register-controlled cartridge lines, and freeze-button support.
/// </summary>
public sealed class C64ActionReplayCartridge : IC64Cartridge, IC64FreezableCartridge
{
    public const int RomBankCount = 4;
    public const int RamSize = 0x2000;

    private const ushort IO1StartAddress = 0xDE00;
    private const ushort IO1EndAddress = 0xDEFF;
    private const ushort IO2StartAddress = 0xDF00;
    private const ushort IO2EndAddress = 0xDFFF;
    private const int IO2WindowOffset = 0x1F00;

    private const byte DisableBit = 0x04;
    private const byte BankMask = 0x18;
    private const byte ExportRamBit = 0x20;

    private static readonly C64CartridgeLines EightKLines = new(GameHigh: true, ExromHigh: false);
    private static readonly C64CartridgeLines SixteenKLines = new(GameHigh: false, ExromHigh: false);
    private static readonly C64CartridgeLines UltimaxLines = new(GameHigh: false, ExromHigh: true);

    private readonly C64BankedRom _rom;
    private readonly byte[] _ram = new byte[RamSize];
    private byte _register;
    private bool _active;
    private bool _freezeMode;

    public C64ActionReplayCartridge(C64BankedRom rom, string name = "Action Replay")
    {
        _rom = rom ?? throw new ArgumentNullException(nameof(rom));
        if (rom.Count != RomBankCount || rom.HighestBank != RomBankCount - 1)
            throw new ArgumentException("Action Replay requires exactly four ROM banks numbered 0-3.", nameof(rom));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Cartridge name must not be empty.", nameof(name));

        Name = name;
        Array.Fill(_ram, (byte)0xFF);
        Reset();
    }

    public string Name { get; }
    public byte Register => _register;
    public ushort CurrentBank
        => _freezeMode ? (ushort)0 : (ushort)((_register & BankMask) >> 3);
    public bool IsActive => _active;
    public bool IsFreezeMode => _freezeMode;
    public bool IsRamExported => _freezeMode || (_register & ExportRamBit) != 0;
    public C64CartridgeLines Lines => GetLines();
    public event Action? LinesChanged;

    public bool HandlesIORead(ushort address)
        => _active && address is >= IO2StartAddress and <= IO2EndAddress;

    public byte ReadIO(ushort address)
    {
        if (!HandlesIORead(address))
            throw new InvalidOperationException($"{Name} does not handle I/O reads at 0x{address:X4}.");

        var offset = IO2WindowOffset + (address & 0xFF);
        return IsRamExported
            ? _ram[offset]
            : _rom.Read(CurrentBank, (ushort)offset);
    }

    public bool HandlesIOWrite(ushort address)
        => _active &&
           (address is >= IO1StartAddress and <= IO1EndAddress ||
            IsRamExported && address is >= IO2StartAddress and <= IO2EndAddress);

    public void WriteIO(ushort address, byte value)
    {
        if (!_active)
            throw new InvalidOperationException($"{Name} is disabled.");

        if (address is >= IO1StartAddress and <= IO1EndAddress)
        {
            WriteControlRegister(value);
            return;
        }

        if (IsRamExported && address is >= IO2StartAddress and <= IO2EndAddress)
        {
            _ram[IO2WindowOffset + (address & 0xFF)] = value;
            return;
        }

        throw new InvalidOperationException($"{Name} does not handle I/O writes at 0x{address:X4}.");
    }

    public bool HasROML => _active && Lines != C64CartridgeLines.Released;

    public byte ReadROML(ushort address)
    {
        if (!HasROML)
            throw new InvalidOperationException($"{Name} does not currently provide ROML.");

        return IsRamExported
            ? _ram[address & (RamSize - 1)]
            : _rom.Read(CurrentBank, address);
    }

    public bool HandlesROMLWrite => HasROML && IsRamExported;

    public void WriteROML(ushort address, byte value)
    {
        if (!HandlesROMLWrite)
            throw new InvalidOperationException($"{Name} ROML is not currently writable.");

        _ram[address & (RamSize - 1)] = value;
    }

    public bool HasROMH => _active && !Lines.GameHigh;

    public byte ReadROMH(ushort address)
        => HasROMH
            ? _rom.Read(CurrentBank, address)
            : throw new InvalidOperationException($"{Name} does not currently provide ROMH.");

    public void Freeze()
    {
        var previousLines = Lines;
        _active = true;
        _freezeMode = true;
        if (previousLines != Lines)
            LinesChanged?.Invoke();
    }

    public void Tick(ulong cyclesElapsed = 0)
    {
    }

    public void Reset()
    {
        var previousLines = Lines;
        _register = 0;
        _active = true;
        _freezeMode = false;
        if (previousLines != Lines)
            LinesChanged?.Invoke();
    }

    public void Dispose()
    {
    }

    public byte ReadRam(ushort offset)
        => _ram[offset & (RamSize - 1)];

    private void WriteControlRegister(byte value)
    {
        var previousLines = Lines;
        _register = value;
        _freezeMode = false;
        if ((value & DisableBit) != 0)
            _active = false;

        if (previousLines != Lines)
            LinesChanged?.Invoke();
    }

    private C64CartridgeLines GetLines()
    {
        if (!_active)
            return C64CartridgeLines.Released;
        if (_freezeMode)
            return UltimaxLines;

        // Original Action Replay hardware has bus contention in register mode $22.
        // VICE treats it as 8K mode while both C64 RAM and cartridge RAM are selected.
        if ((_register & 0x23) == 0x22)
            return EightKLines;

        return (_register & 0x03) switch
        {
            0 => EightKLines,
            1 => SixteenKLines,
            2 => C64CartridgeLines.Released,
            3 => UltimaxLines,
            _ => throw new InvalidOperationException(),
        };
    }
}
