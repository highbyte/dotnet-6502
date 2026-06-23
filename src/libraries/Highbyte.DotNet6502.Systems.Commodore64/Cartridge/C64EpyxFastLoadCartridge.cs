namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// Epyx FastLoad cartridge with a capacitor-style ROM timeout.
/// ROML and IO1 reads enable ROML for 512 CPU cycles; IO2 always exposes the final ROM page.
/// </summary>
public sealed class C64EpyxFastLoadCartridge : IC64Cartridge
{
    public const int RomSize = 0x2000;
    public const ulong RomEnableCycles = 512;

    private const ushort IO1StartAddress = 0xDE00;
    private const ushort IO1EndAddress = 0xDEFF;
    private const ushort IO2StartAddress = 0xDF00;
    private const ushort IO2EndAddress = 0xDFFF;
    private const int IO2RomOffset = 0x1F00;

    private static readonly C64CartridgeLines EnabledLines = new(GameHigh: true, ExromHigh: false);

    private readonly byte[] _rom;
    private ulong _cyclesUntilDisabled;

    public C64EpyxFastLoadCartridge(byte[] rom, string name = "Epyx FastLoad")
    {
        ArgumentNullException.ThrowIfNull(rom);
        if (rom.Length != RomSize)
            throw new ArgumentException($"Epyx FastLoad ROM must be exactly {RomSize} bytes.", nameof(rom));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Cartridge name must not be empty.", nameof(name));

        _rom = rom.ToArray();
        Name = name;
        Reset();
    }

    public string Name { get; }
    public bool IsRomEnabled => _cyclesUntilDisabled > 0;
    public ulong CyclesUntilDisabled => _cyclesUntilDisabled;
    public C64CartridgeLines Lines => IsRomEnabled ? EnabledLines : C64CartridgeLines.Released;
    public event Action? LinesChanged;

    public bool HandlesIORead(ushort address)
        => address is >= IO1StartAddress and <= IO2EndAddress;

    public byte ReadIO(ushort address)
    {
        if (address is >= IO1StartAddress and <= IO1EndAddress)
        {
            TriggerRom();
            return 0;
        }

        if (address is >= IO2StartAddress and <= IO2EndAddress)
            return _rom[IO2RomOffset + (address & 0xFF)];

        throw new ArgumentOutOfRangeException(nameof(address));
    }

    public bool HandlesIOWrite(ushort address) => false;
    public void WriteIO(ushort address, byte value)
        => throw new InvalidOperationException($"{Name} has no writable cartridge I/O registers.");

    public bool HasROML => IsRomEnabled;

    public byte ReadROML(ushort address)
    {
        if (!IsRomEnabled)
            throw new InvalidOperationException($"{Name} ROML is disabled.");

        TriggerRom();
        return _rom[address & (RomSize - 1)];
    }

    public bool HasROMH => false;
    public byte ReadROMH(ushort address)
        => throw new InvalidOperationException($"{Name} does not provide ROMH.");

    public void Tick(ulong cyclesElapsed = 0)
    {
        if (!IsRomEnabled || cyclesElapsed == 0)
            return;

        if (cyclesElapsed < _cyclesUntilDisabled)
        {
            _cyclesUntilDisabled -= cyclesElapsed;
            return;
        }

        _cyclesUntilDisabled = 0;
        LinesChanged?.Invoke();
    }

    public void Reset()
        => TriggerRom();

    public void Dispose()
    {
    }

    private void TriggerRom()
    {
        var wasEnabled = IsRomEnabled;
        _cyclesUntilDisabled = RomEnableCycles;
        if (!wasEnabled)
            LinesChanged?.Invoke();
    }
}
