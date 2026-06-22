namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// Ocean game cartridge with banked 8K ROM and a write-only IO1 bank register.
/// Smaller cartridges use 16K mode with the selected bank mirrored in ROML and ROMH;
/// 512K cartridges use 8K ROML-only mode.
/// </summary>
public sealed class C64OceanCartridge : IC64Cartridge
{
    private const ushort IO1StartAddress = 0xDE00;
    private const ushort IO1EndAddress = 0xDEFF;

    private static readonly C64CartridgeLines EightKLines = new(GameHigh: true, ExromHigh: false);
    private static readonly C64CartridgeLines SixteenKLines = new(GameHigh: false, ExromHigh: false);

    private readonly C64BankedRom _rom;
    private readonly byte _bankMask;
    private byte _register;

    public C64OceanCartridge(
        C64BankedRom rom,
        bool useEightKMode,
        string name = "Ocean")
    {
        _rom = rom ?? throw new ArgumentNullException(nameof(rom));
        if (rom.Count > 64)
            throw new ArgumentException("Ocean supports at most 64 ROM banks.", nameof(rom));
        if (!IsPowerOfTwo(rom.Count))
            throw new ArgumentException("Ocean ROM bank count must be a power of two.", nameof(rom));
        if (useEightKMode && rom.Count != 64)
            throw new ArgumentException("Ocean 8K mode requires exactly 64 ROM banks.", nameof(useEightKMode));
        if (!useEightKMode && rom.Count == 64)
            throw new ArgumentException("A 64-bank Ocean cartridge must use 8K mode.", nameof(useEightKMode));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Cartridge name must not be empty.", nameof(name));

        UseEightKMode = useEightKMode;
        Name = name;
        _bankMask = (byte)(rom.Count - 1);
        Reset();
    }

    public string Name { get; }
    public bool UseEightKMode { get; }
    public C64CartridgeLines Lines => UseEightKMode ? EightKLines : SixteenKLines;
    public event Action? LinesChanged
    {
        add { }
        remove { }
    }

    public ushort CurrentBank => (ushort)(_register & _bankMask & 0x3F);

    public bool HandlesIORead(ushort address) => false;
    public byte ReadIO(ushort address)
        => throw new InvalidOperationException($"{Name} has no readable cartridge I/O registers.");

    public bool HandlesIOWrite(ushort address)
        => address is >= IO1StartAddress and <= IO1EndAddress;

    public void WriteIO(ushort address, byte value)
        => _register = value;

    public bool HasROML => true;
    public byte ReadROML(ushort address) => _rom.Read(CurrentBank, address);

    public bool HasROMH => !UseEightKMode;
    public byte ReadROMH(ushort address)
        => HasROMH
            ? _rom.Read(CurrentBank, address)
            : throw new InvalidOperationException($"{Name} does not provide ROMH in 8K mode.");

    public void Tick()
    {
    }

    public void Reset()
        => _register = 0;

    public void Dispose()
    {
    }

    private static bool IsPowerOfTwo(int value)
        => value > 0 && (value & (value - 1)) == 0;
}
