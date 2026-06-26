namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

public sealed class C64CartridgeSlot : IDisposable
{
    private const ushort IO1StartAddress = 0xDE00;
    private const ushort IO2EndAddress = 0xDFFF;
    private const ushort ROMLStartAddress = 0x8000;
    private const ushort CartridgeRomWindowSize = 0x2000;

    public IC64Cartridge? AttachedCartridge { get; private set; }
    public C64CartridgeLines Lines => AttachedCartridge?.Lines ?? C64CartridgeLines.Released;
    public bool NmiLineActive
        => AttachedCartridge is IC64CartridgeNmiSource { NmiLineActive: true };
    public bool IrqLineActive
        => AttachedCartridge is IC64CartridgeIrqSource { IrqLineActive: true };
    public event Action? LinesChanged;
    public event Action? NmiLineChanged;
    public event Action? IrqLineChanged;

    public void Attach(IC64Cartridge cartridge)
    {
        ArgumentNullException.ThrowIfNull(cartridge);

        if (AttachedCartridge != null)
            throw new InvalidOperationException($"Cartridge '{AttachedCartridge.Name}' is already attached.");

        cartridge.Reset();
        AttachedCartridge = cartridge;
        cartridge.LinesChanged += OnCartridgeLinesChanged;
        SubscribeToNmiSource(cartridge);
        SubscribeToIrqSource(cartridge);
        LinesChanged?.Invoke();
        if (cartridge is IC64CartridgeNmiSource)
            NmiLineChanged?.Invoke();
        if (cartridge is IC64CartridgeIrqSource)
            IrqLineChanged?.Invoke();
    }

    public void Replace(IC64Cartridge cartridge)
    {
        ArgumentNullException.ThrowIfNull(cartridge);
        cartridge.Reset();

        var previous = AttachedCartridge;
        if (previous != null)
        {
            previous.LinesChanged -= OnCartridgeLinesChanged;
            UnsubscribeFromNmiSource(previous);
            UnsubscribeFromIrqSource(previous);
        }

        AttachedCartridge = cartridge;
        cartridge.LinesChanged += OnCartridgeLinesChanged;
        SubscribeToNmiSource(cartridge);
        SubscribeToIrqSource(cartridge);
        LinesChanged?.Invoke();
        if (previous is IC64CartridgeNmiSource || cartridge is IC64CartridgeNmiSource)
            NmiLineChanged?.Invoke();
        if (previous is IC64CartridgeIrqSource || cartridge is IC64CartridgeIrqSource)
            IrqLineChanged?.Invoke();

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
        if (cartridge is IC64CartridgeNmiSource)
            NmiLineChanged?.Invoke();
        if (cartridge is IC64CartridgeIrqSource)
            IrqLineChanged?.Invoke();
        cartridge.LinesChanged -= OnCartridgeLinesChanged;
        UnsubscribeFromNmiSource(cartridge);
        UnsubscribeFromIrqSource(cartridge);
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
        => MapROMLLocations(mem, fallbackReader, fallbackWriter: null);

    public void MapROMLLocations(
        Memory mem,
        Func<ushort, byte> fallbackReader,
        Action<ushort, byte>? fallbackWriter)
    {
        MapRomWindow(
            mem,
            ROMLStartAddress,
            fallbackReader,
            cartridge => cartridge.HasROML,
            (cartridge, address) => cartridge.ReadROML(address));

        if (fallbackWriter == null)
            return;

        Memory.StoreByte writer = (mappedAddress, value) =>
        {
            var cartridge = AttachedCartridge;
            if (cartridge?.HandlesROMLWrite == true)
                cartridge.WriteROML(mappedAddress, value);
            else
                fallbackWriter(mappedAddress, value);
        };
        for (var address = (int)ROMLStartAddress;
             address < ROMLStartAddress + CartridgeRomWindowSize;
             address++)
        {
            mem.MapWriter((ushort)address, writer);
        }
    }

    public void MapROMHLocations(
        Memory mem,
        ushort baseAddress,
        Func<ushort, byte> fallbackReader)
        => MapROMHLocations(mem, baseAddress, fallbackReader, fallbackWriter: null);

    public void MapROMHLocations(
        Memory mem,
        ushort baseAddress,
        Func<ushort, byte> fallbackReader,
        Action<ushort, byte>? fallbackWriter,
        Func<ushort, byte>? normalMemoryFallbackReader = null)
    {
        MapRomWindow(
            mem,
            baseAddress,
            fallbackReader,
            cartridge => cartridge.HasROMH,
            (cartridge, address) => cartridge.ReadROMH(address),
            normalMemoryFallbackReader,
            (cartridge, address) =>
                cartridge is IC64CartridgeNormalMemoryFallback normalFallback &&
                normalFallback.UsesNormalMemoryFallbackForROMH(address));

        if (fallbackWriter == null)
            return;

        Memory.StoreByte writer = (mappedAddress, value) => fallbackWriter(mappedAddress, value);
        var endAddress = baseAddress + CartridgeRomWindowSize;
        for (var address = (int)baseAddress; address < endAddress; address++)
            mem.MapWriter((ushort)address, writer);
    }

    public void Tick(ulong cyclesElapsed = 0)
        => AttachedCartridge?.Tick(cyclesElapsed);

    public void Reset()
        => AttachedCartridge?.Reset();

    public bool Freeze()
    {
        if (AttachedCartridge is not IC64FreezableCartridge freezableCartridge)
            return false;

        freezableCartridge.Freeze();
        return true;
    }

    public void AcknowledgeNmi()
    {
        if (AttachedCartridge is IC64CartridgeNmiAcknowledgeHandler handler)
            handler.AcknowledgeNmi();
    }

    public void Dispose()
        => Detach();

    private void OnCartridgeLinesChanged()
        => LinesChanged?.Invoke();

    private void OnCartridgeNmiLineChanged()
        => NmiLineChanged?.Invoke();

    private void OnCartridgeIrqLineChanged()
        => IrqLineChanged?.Invoke();

    private void SubscribeToNmiSource(IC64Cartridge cartridge)
    {
        if (cartridge is IC64CartridgeNmiSource nmiSource)
            nmiSource.NmiLineChanged += OnCartridgeNmiLineChanged;
    }

    private void UnsubscribeFromNmiSource(IC64Cartridge cartridge)
    {
        if (cartridge is IC64CartridgeNmiSource nmiSource)
            nmiSource.NmiLineChanged -= OnCartridgeNmiLineChanged;
    }

    private void SubscribeToIrqSource(IC64Cartridge cartridge)
    {
        if (cartridge is IC64CartridgeIrqSource irqSource)
            irqSource.IrqLineChanged += OnCartridgeIrqLineChanged;
    }

    private void UnsubscribeFromIrqSource(IC64Cartridge cartridge)
    {
        if (cartridge is IC64CartridgeIrqSource irqSource)
            irqSource.IrqLineChanged -= OnCartridgeIrqLineChanged;
    }

    private byte ReadIO(ushort address, Func<ushort, byte> fallbackReader)
    {
        var cartridge = AttachedCartridge;
        if (cartridge?.HandlesIORead(address) != true)
            return fallbackReader(address);

        var cartridgeValue = cartridge.ReadIO(address);
        return cartridge.ProvidesIOReadValue(address)
            ? cartridgeValue
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
        Func<IC64Cartridge, ushort, byte> cartridgeReader,
        Func<ushort, byte>? normalMemoryFallbackReader = null,
        Func<IC64Cartridge, ushort, bool>? usesNormalMemoryFallback = null)
    {
        var endAddress = baseAddress + CartridgeRomWindowSize;
        Memory.LoadByte reader =
            normalMemoryFallbackReader == null || usesNormalMemoryFallback == null
                ? mappedAddress =>
                {
                    var cartridge = AttachedCartridge;
                    return cartridge != null && isAvailable(cartridge)
                        ? cartridgeReader(cartridge, mappedAddress)
                        : fallbackReader(mappedAddress);
                }
                : mappedAddress =>
                {
                    var cartridge = AttachedCartridge;
                    if (cartridge != null && isAvailable(cartridge))
                        return cartridgeReader(cartridge, mappedAddress);

                    if (cartridge != null && usesNormalMemoryFallback(cartridge, mappedAddress))
                        return normalMemoryFallbackReader(mappedAddress);

                    return fallbackReader(mappedAddress);
                };
        for (var address = (int)baseAddress; address < endAddress; address++)
            mem.MapReader((ushort)address, reader);
    }
}
