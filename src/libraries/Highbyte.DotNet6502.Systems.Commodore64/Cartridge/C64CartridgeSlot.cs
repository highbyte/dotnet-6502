namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

public sealed class C64CartridgeSlot : IDisposable
{
    private const ushort IO1StartAddress = 0xDE00;
    private const ushort IO2EndAddress = 0xDFFF;
    private const ushort ROMLStartAddress = 0x8000;
    private const ushort CartridgeRomWindowSize = 0x2000;

    public IC64Cartridge? AttachedCartridge { get; private set; }
    public C64CartridgeLines Lines => AttachedCartridge?.Lines ?? C64CartridgeLines.Released;
    public event Action? LinesChanged;

    public void Attach(IC64Cartridge cartridge)
    {
        ArgumentNullException.ThrowIfNull(cartridge);

        if (AttachedCartridge != null)
            throw new InvalidOperationException($"Cartridge '{AttachedCartridge.Name}' is already attached.");

        cartridge.Reset();
        AttachedCartridge = cartridge;
        cartridge.LinesChanged += OnCartridgeLinesChanged;
        LinesChanged?.Invoke();
    }

    public void Replace(IC64Cartridge cartridge)
    {
        ArgumentNullException.ThrowIfNull(cartridge);
        cartridge.Reset();

        var previous = AttachedCartridge;
        if (previous != null)
            previous.LinesChanged -= OnCartridgeLinesChanged;

        AttachedCartridge = cartridge;
        cartridge.LinesChanged += OnCartridgeLinesChanged;
        LinesChanged?.Invoke();

        if (previous != null)
        {
            previous.Reset();
            previous.Dispose();
        }
    }

    public void Detach()
    {
        var cartridge = AttachedCartridge;
        AttachedCartridge = null;
        if (cartridge == null)
            return;

        LinesChanged?.Invoke();
        cartridge.LinesChanged -= OnCartridgeLinesChanged;
        cartridge.Reset();
        cartridge.Dispose();
    }

    public TCartridge? GetCartridge<TCartridge>()
        where TCartridge : class, IC64Cartridge
        => AttachedCartridge as TCartridge;

    public void MapIOLocations(
        Memory mem,
        Func<ushort, byte> fallbackReader,
        Action<ushort, byte> fallbackWriter)
    {
        Memory.LoadByte reader = ioAddress => ReadIO(ioAddress, fallbackReader);
        Memory.StoreByte writer = (ioAddress, value) => WriteIO(ioAddress, value, fallbackWriter);
        for (var address = (int)IO1StartAddress; address <= IO2EndAddress; address++)
        {
            var mappedAddress = (ushort)address;
            mem.MapReader(mappedAddress, reader);
            mem.MapWriter(mappedAddress, writer);
        }
    }

    public void MapROMLLocations(Memory mem, Func<ushort, byte> fallbackReader)
    {
        MapRomWindow(
            mem,
            ROMLStartAddress,
            fallbackReader,
            cartridge => cartridge.HasROML,
            (cartridge, address) => cartridge.ReadROML(address));
    }

    public void MapROMHLocations(
        Memory mem,
        ushort baseAddress,
        Func<ushort, byte> fallbackReader)
    {
        MapRomWindow(
            mem,
            baseAddress,
            fallbackReader,
            cartridge => cartridge.HasROMH,
            (cartridge, address) => cartridge.ReadROMH(address));
    }

    public void Tick()
        => AttachedCartridge?.Tick();

    public void Reset()
        => AttachedCartridge?.Reset();

    public void Dispose()
        => Detach();

    private void OnCartridgeLinesChanged()
        => LinesChanged?.Invoke();

    private byte ReadIO(ushort address, Func<ushort, byte> fallbackReader)
    {
        var cartridge = AttachedCartridge;
        return cartridge?.HandlesIORead(address) == true
            ? cartridge.ReadIO(address)
            : fallbackReader(address);
    }

    private void WriteIO(ushort address, byte value, Action<ushort, byte> fallbackWriter)
    {
        var cartridge = AttachedCartridge;
        if (cartridge?.HandlesIOWrite(address) == true)
            cartridge.WriteIO(address, value);
        else
            fallbackWriter(address, value);
    }

    private void MapRomWindow(
        Memory mem,
        ushort baseAddress,
        Func<ushort, byte> fallbackReader,
        Func<IC64Cartridge, bool> isAvailable,
        Func<IC64Cartridge, ushort, byte> cartridgeReader)
    {
        var endAddress = baseAddress + CartridgeRomWindowSize;
        Memory.LoadByte reader = mappedAddress =>
        {
            var cartridge = AttachedCartridge;
            return cartridge != null && isAvailable(cartridge)
                ? cartridgeReader(cartridge, mappedAddress)
                : fallbackReader(mappedAddress);
        };
        for (var address = (int)baseAddress; address < endAddress; address++)
            mem.MapReader((ushort)address, reader);
    }
}
