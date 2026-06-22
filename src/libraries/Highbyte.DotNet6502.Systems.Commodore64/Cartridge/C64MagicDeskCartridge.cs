namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// Magic Desk compatible cartridge with banked 8K ROML and a write-only IO1 register.
/// </summary>
public sealed class C64MagicDeskCartridge : IC64Cartridge
{
    private const ushort IO1StartAddress = 0xDE00;
    private const ushort IO1EndAddress = 0xDEFF;
    private const byte DisableBit = 0x80;

    private static readonly C64CartridgeLines EnabledLines = new(GameHigh: true, ExromHigh: false);

    private readonly C64BankedRom _rom;
    private readonly byte _bankMask;
    private byte _register;

    public C64MagicDeskCartridge(C64BankedRom rom, string name = "Magic Desk")
    {
        _rom = rom ?? throw new ArgumentNullException(nameof(rom));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Cartridge name must not be empty.", nameof(name));

        Name = name;
        _bankMask = GetBankMask(rom.HighestBank);
        Reset();
    }

    public string Name { get; }
    public C64CartridgeLines Lines
        => IsEnabled ? EnabledLines : C64CartridgeLines.Released;
    public event Action? LinesChanged;

    public ushort CurrentBank => (ushort)(_register & _bankMask);
    public bool IsEnabled => (_register & DisableBit) == 0;

    public bool HandlesIORead(ushort address) => false;
    public byte ReadIO(ushort address)
        => throw new InvalidOperationException($"{Name} has no readable cartridge I/O registers.");

    public bool HandlesIOWrite(ushort address)
        => address is >= IO1StartAddress and <= IO1EndAddress;

    public void WriteIO(ushort address, byte value)
    {
        var wasEnabled = IsEnabled;
        _register = (byte)(value & (DisableBit | _bankMask));
        if (wasEnabled != IsEnabled)
            LinesChanged?.Invoke();
    }

    public bool HasROML => true;
    public byte ReadROML(ushort address) => _rom.Read(CurrentBank, address);
    public bool HasROMH => false;
    public byte ReadROMH(ushort address)
        => throw new InvalidOperationException($"{Name} does not provide ROMH.");

    public void Tick()
    {
    }

    public void Reset()
    {
        var linesChanged = !IsEnabled;
        _register = 0;
        if (linesChanged)
            LinesChanged?.Invoke();
    }

    public void Dispose()
    {
    }

    private static byte GetBankMask(ushort highestBank)
    {
        if (highestBank > 127)
            throw new ArgumentOutOfRangeException(nameof(highestBank), "Magic Desk supports at most 128 ROM banks.");
        if (highestBank <= 3)
            return 0x03;
        if (highestBank <= 7)
            return 0x07;
        if (highestBank <= 15)
            return 0x0F;
        if (highestBank <= 31)
            return 0x1F;
        if (highestBank <= 63)
            return 0x3F;
        return 0x7F;
    }
}
