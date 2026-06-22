namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// A standard fixed 8K or 16K C64 ROM cartridge. Supplying only ROML selects 8K mode;
/// supplying both ROML and ROMH selects 16K mode.
/// </summary>
public sealed class C64RomCartridge : IC64Cartridge
{
    public const int RomWindowSize = 0x2000;

    private readonly byte[] _roml;
    private readonly byte[]? _romh;

    public C64RomCartridge(byte[] roml, byte[]? romh = null, string name = "ROM cartridge")
    {
        ArgumentNullException.ThrowIfNull(roml);
        if (roml.Length != RomWindowSize)
            throw new ArgumentException($"ROML must be exactly {RomWindowSize} bytes.", nameof(roml));
        if (romh != null && romh.Length != RomWindowSize)
            throw new ArgumentException($"ROMH must be exactly {RomWindowSize} bytes.", nameof(romh));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Cartridge name must not be empty.", nameof(name));

        _roml = roml.ToArray();
        _romh = romh?.ToArray();
        Name = name;
        Lines = romh == null
            ? new C64CartridgeLines(GameHigh: true, ExromHigh: false)
            : new C64CartridgeLines(GameHigh: false, ExromHigh: false);
    }

    public string Name { get; }
    public C64CartridgeLines Lines { get; }
    public event Action? LinesChanged
    {
        add { }
        remove { }
    }

    public bool HandlesIOAddress(ushort address) => false;
    public byte ReadIO(ushort address) => throw new InvalidOperationException($"{Name} does not provide cartridge I/O.");
    public void WriteIO(ushort address, byte value) => throw new InvalidOperationException($"{Name} does not provide cartridge I/O.");

    public bool HasROML => true;
    public byte ReadROML(ushort address) => _roml[address & (RomWindowSize - 1)];

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
