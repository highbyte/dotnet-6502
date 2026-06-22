namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// A fixed C64 ROM cartridge with explicit ROML/ROMH windows and cartridge lines.
/// </summary>
public sealed class C64RomCartridge : IC64Cartridge
{
    public const int RomWindowSize = 0x2000;

    private readonly byte[]? _roml;
    private readonly byte[]? _romh;

    public C64RomCartridge(byte[] roml, byte[]? romh = null, string name = "ROM cartridge")
        : this(
            roml,
            romh,
            romh == null
                ? new C64CartridgeLines(GameHigh: true, ExromHigh: false)
                : new C64CartridgeLines(GameHigh: false, ExromHigh: false),
            name)
    {
    }

    public C64RomCartridge(
        byte[]? roml,
        byte[]? romh,
        C64CartridgeLines lines,
        string name = "ROM cartridge")
    {
        if (roml == null && romh == null)
            throw new ArgumentException("At least one cartridge ROM window must be supplied.");
        if (roml != null && roml.Length != RomWindowSize)
            throw new ArgumentException($"ROML must be exactly {RomWindowSize} bytes.", nameof(roml));
        if (romh != null && romh.Length != RomWindowSize)
            throw new ArgumentException($"ROMH must be exactly {RomWindowSize} bytes.", nameof(romh));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Cartridge name must not be empty.", nameof(name));

        _roml = roml?.ToArray();
        _romh = romh?.ToArray();
        Name = name;
        Lines = lines;
    }

    public string Name { get; }
    public C64CartridgeLines Lines { get; }
    public event Action? LinesChanged
    {
        add { }
        remove { }
    }

    public bool HandlesIORead(ushort address) => false;
    public byte ReadIO(ushort address) => throw new InvalidOperationException($"{Name} does not provide cartridge I/O.");
    public bool HandlesIOWrite(ushort address) => false;
    public void WriteIO(ushort address, byte value) => throw new InvalidOperationException($"{Name} does not provide cartridge I/O.");

    public bool HasROML => _roml != null;
    public byte ReadROML(ushort address)
        => _roml?[address & (RomWindowSize - 1)]
            ?? throw new InvalidOperationException($"{Name} does not provide ROML.");

    public bool HasROMH => _romh != null;
    public byte ReadROMH(ushort address)
        => _romh?[address & (RomWindowSize - 1)]
            ?? throw new InvalidOperationException($"{Name} does not provide ROMH.");

    public void Tick()
    {
    }

    public void Reset()
    {
    }

    public void Dispose()
    {
    }
}
