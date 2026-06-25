namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// Trilogic Expert Cartridge saved-RAM image support.
/// </summary>
public sealed class C64ExpertCartridge :
    IC64Cartridge,
    IC64FreezableCartridge,
    IC64CartridgeNmiAcknowledgeHandler,
    IC64CartridgeNormalMemoryFallback
{
    public const int RamSize = 0x2000;

    private const ushort IO1ToggleStartAddress = 0xDE00;
    private const ushort IO1ToggleEndAddress = 0xDE01;

    private static readonly C64CartridgeLines EightKLines = new(GameHigh: true, ExromHigh: false);
    private static readonly C64CartridgeLines UltimaxLines = new(GameHigh: false, ExromHigh: true);

    private readonly byte[] _ram;
    private C64ExpertMode _mode;
    private bool _registerEnabled;
    private bool _ramWriteable;
    private bool _ramVisible;

    public C64ExpertCartridge(
        byte[] ramImage,
        string name = "Expert Cartridge",
        C64ExpertMode mode = C64ExpertMode.On)
    {
        ArgumentNullException.ThrowIfNull(ramImage);
        if (ramImage.Length != RamSize)
            throw new ArgumentException($"Expert Cartridge requires exactly {RamSize} bytes of RAM image data.", nameof(ramImage));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Cartridge name must not be empty.", nameof(name));

        _ram = ramImage.ToArray();
        Name = name;
        _mode = mode;
        Reset();
    }

    public string Name { get; }
    public C64ExpertMode Mode => _mode;
    public bool IsRegisterEnabled => _registerEnabled;
    public bool IsRamWriteable => _ramWriteable;
    public bool IsRamVisible => _ramVisible;

    public C64CartridgeLines Lines
        => _mode switch
        {
            C64ExpertMode.Prg => EightKLines,
            C64ExpertMode.On => UltimaxLines,
            _ => C64CartridgeLines.Released,
        };

    public event Action? LinesChanged;

    public void SetMode(C64ExpertMode mode)
    {
        if (_mode == mode)
            return;

        var previousLines = Lines;
        _mode = mode;

        switch (_mode)
        {
            case C64ExpertMode.Prg:
                _registerEnabled = true;
                _ramWriteable = true;
                _ramVisible = true;
                break;
            case C64ExpertMode.On:
            case C64ExpertMode.Off:
                _registerEnabled = false;
                _ramWriteable = false;
                _ramVisible = false;
                break;
            default:
                throw new InvalidOperationException($"Unsupported Expert mode {_mode}.");
        }

        if (previousLines != Lines)
            LinesChanged?.Invoke();
    }

    public bool HandlesIORead(ushort address)
        => address is >= IO1ToggleStartAddress and <= IO1ToggleEndAddress;

    public byte ReadIO(ushort address)
    {
        if (!HandlesIORead(address))
            throw new InvalidOperationException($"{Name} does not handle I/O reads at 0x{address:X4}.");

        ToggleRegisterIfActive();
        return 0;
    }

    public bool ProvidesIOReadValue(ushort address)
        => false;

    public bool HandlesIOWrite(ushort address)
        => address is >= IO1ToggleStartAddress and <= IO1ToggleEndAddress;

    public void WriteIO(ushort address, byte value)
    {
        if (!HandlesIOWrite(address))
            throw new InvalidOperationException($"{Name} does not handle I/O writes at 0x{address:X4}.");

        ToggleRegisterIfActive();
    }

    public bool HasROML
        => _mode == C64ExpertMode.Prg ||
           _mode == C64ExpertMode.On && _ramVisible;

    public byte ReadROML(ushort address)
        => HasROML
            ? _ram[address & (RamSize - 1)]
            : throw new InvalidOperationException($"{Name} does not currently provide ROML.");

    public bool HandlesROMLWrite => HasROML && _ramWriteable;

    public void WriteROML(ushort address, byte value)
    {
        if (!HandlesROMLWrite)
            throw new InvalidOperationException($"{Name} ROML is not currently writable.");

        _ram[address & (RamSize - 1)] = value;
    }

    public bool HasROMH
        => _mode == C64ExpertMode.On && _ramVisible;

    public byte ReadROMH(ushort address)
        => HasROMH
            ? _ram[address & (RamSize - 1)]
            : throw new InvalidOperationException($"{Name} does not currently provide ROMH.");

    public bool UsesNormalMemoryFallbackForROMH(ushort address)
        => _mode == C64ExpertMode.On && !_ramVisible;

    public void Freeze()
    {
        MapWritableRamOnNmi();
    }

    public void AcknowledgeNmi()
    {
        MapWritableRamOnNmi();
    }

    public void Tick(ulong cyclesElapsed = 0)
    {
    }

    public void Reset()
    {
        var previousLines = Lines;
        switch (_mode)
        {
            case C64ExpertMode.Prg:
                _registerEnabled = true;
                _ramWriteable = true;
                _ramVisible = true;
                break;
            case C64ExpertMode.On:
                _registerEnabled = true;
                _ramWriteable = true;
                _ramVisible = true;
                break;
            case C64ExpertMode.Off:
                _registerEnabled = false;
                _ramWriteable = false;
                _ramVisible = false;
                break;
            default:
                throw new InvalidOperationException($"Unsupported Expert mode {_mode}.");
        }

        if (previousLines != Lines)
            LinesChanged?.Invoke();
    }

    public void Dispose()
    {
    }

    public byte ReadRam(ushort offset)
        => _ram[offset & (RamSize - 1)];

    private void ToggleRegisterIfActive()
    {
        if (_mode != C64ExpertMode.On || !_registerEnabled)
            return;

        var previousLines = Lines;
        _ramVisible = !_ramVisible;
        _ramWriteable = false;

        if (previousLines != Lines)
            LinesChanged?.Invoke();
    }

    private void MapWritableRamOnNmi()
    {
        if (_mode != C64ExpertMode.On)
            return;

        var previousLines = Lines;
        _registerEnabled = true;
        _ramWriteable = true;
        _ramVisible = true;

        if (previousLines != Lines)
            LinesChanged?.Invoke();
    }
}

public enum C64ExpertMode
{
    Off,
    Prg,
    On,
}
