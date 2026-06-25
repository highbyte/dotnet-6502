namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// The Final Cartridge III, including the 16-bank community FC3+ extension.
/// </summary>
public sealed class C64FinalCartridgeIIICartridge :
    IC64Cartridge,
    IC64FreezableCartridge,
    IC64CartridgeNmiSource
{
    public const int StandardRomBankCount = 4;
    public const int ExtendedRomBankCount = 16;
    public const int CrtBankSize = 0x4000;

    private const ushort IO1StartAddress = 0xDE00;
    private const ushort IO1EndAddress = 0xDEFF;
    private const ushort IO2StartAddress = 0xDF00;
    private const ushort IO2EndAddress = 0xDFFF;
    private const ushort ControlRegisterAddress = 0xDFFF;
    private const ushort IO1RomOffset = 0x1E00;
    private const ushort IO2RomOffset = 0x1F00;

    private const byte ExromHighBit = 0x10;
    private const byte GameHighBit = 0x20;
    private const byte NmiReleasedBit = 0x40;
    private const byte HideRegisterBit = 0x80;

    private static readonly C64CartridgeLines UltimaxLines = new(GameHigh: false, ExromHigh: true);

    private readonly C64BankedRom _roml;
    private readonly C64BankedRom _romh;
    private readonly ushort _bankMask;
    private byte _register;
    private bool _registerEnabled;
    private bool _freezeMode;
    private bool _nmiLineActive;

    public C64FinalCartridgeIIICartridge(
        C64BankedRom roml,
        C64BankedRom romh,
        int bankCount,
        string name = "The Final Cartridge III")
    {
        _roml = roml ?? throw new ArgumentNullException(nameof(roml));
        _romh = romh ?? throw new ArgumentNullException(nameof(romh));
        if (bankCount is not (StandardRomBankCount or ExtendedRomBankCount))
            throw new ArgumentOutOfRangeException(nameof(bankCount), "Final Cartridge III requires 4 or 16 ROM banks.");
        if (roml.Count != bankCount || romh.Count != bankCount ||
            roml.HighestBank != bankCount - 1 || romh.HighestBank != bankCount - 1)
        {
            throw new ArgumentException("Final Cartridge III ROML and ROMH must contain every bank.");
        }
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Cartridge name must not be empty.", nameof(name));

        _bankMask = (ushort)(bankCount - 1);
        Name = name;
        Reset();
    }

    public string Name { get; }
    public byte Register => _register;
    public ushort CurrentBank => (ushort)(_register & _bankMask);
    public bool IsRegisterEnabled => _registerEnabled;
    public bool IsFreezeMode => _freezeMode;
    public bool NmiLineActive => _nmiLineActive;
    public C64CartridgeLines Lines
        => _freezeMode
            ? UltimaxLines
            : new C64CartridgeLines(
                GameHigh: (_register & GameHighBit) != 0,
                ExromHigh: (_register & ExromHighBit) != 0);

    public event Action? LinesChanged;
    public event Action? NmiLineChanged;

    public bool HandlesIORead(ushort address)
        => address is >= IO1StartAddress and <= IO2EndAddress;

    public byte ReadIO(ushort address)
    {
        if (address is >= IO1StartAddress and <= IO1EndAddress)
            return _roml.Read(CurrentBank, (ushort)(IO1RomOffset + (address & 0xFF)));
        if (address is >= IO2StartAddress and <= IO2EndAddress)
            return _roml.Read(CurrentBank, (ushort)(IO2RomOffset + (address & 0xFF)));

        throw new InvalidOperationException($"{Name} does not handle I/O reads at 0x{address:X4}.");
    }

    public bool HandlesIOWrite(ushort address)
        => address is >= IO2StartAddress and <= IO2EndAddress;

    public void WriteIO(ushort address, byte value)
    {
        if (!HandlesIOWrite(address))
            throw new InvalidOperationException($"{Name} does not handle I/O writes at 0x{address:X4}.");
        if (address != ControlRegisterAddress || !_registerEnabled)
            return;

        var previousLines = Lines;
        var previousNmiLineActive = _nmiLineActive;

        _register = value;
        _registerEnabled = (value & HideRegisterBit) == 0;
        _freezeMode = false;
        _nmiLineActive = (value & NmiReleasedBit) == 0;

        if (previousLines != Lines)
            LinesChanged?.Invoke();
        if (previousNmiLineActive != _nmiLineActive)
            NmiLineChanged?.Invoke();
    }

    public bool HasROML => Lines != C64CartridgeLines.Released;

    public byte ReadROML(ushort address)
        => HasROML
            ? _roml.Read(CurrentBank, address)
            : throw new InvalidOperationException($"{Name} does not currently provide ROML.");

    public bool HasROMH => !Lines.GameHigh;

    public byte ReadROMH(ushort address)
        => HasROMH
            ? _romh.Read(CurrentBank, address)
            : throw new InvalidOperationException($"{Name} does not currently provide ROMH.");

    public void Freeze()
    {
        var previousLines = Lines;
        var previousNmiLineActive = _nmiLineActive;
        _registerEnabled = true;
        _freezeMode = true;
        _nmiLineActive = true;
        if (previousLines != Lines)
            LinesChanged?.Invoke();
        if (previousNmiLineActive != _nmiLineActive)
            NmiLineChanged?.Invoke();
    }

    public void Tick(ulong cyclesElapsed = 0)
    {
    }

    public void Reset()
    {
        var previousLines = Lines;
        var previousNmiLineActive = _nmiLineActive;

        _register = 0;
        _registerEnabled = true;
        _freezeMode = false;
        _nmiLineActive = false;

        if (previousLines != Lines)
            LinesChanged?.Invoke();
        if (previousNmiLineActive != _nmiLineActive)
            NmiLineChanged?.Invoke();
    }

    public void Dispose()
    {
    }
}
