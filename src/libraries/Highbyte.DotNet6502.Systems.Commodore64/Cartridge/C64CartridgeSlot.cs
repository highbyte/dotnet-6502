namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

public sealed class C64CartridgeSlot : IDisposable
{
    private const ushort IO1StartAddress = 0xDE00;
    private const ushort IO2EndAddress = 0xDFFF;

    public IC64Cartridge? AttachedCartridge { get; private set; }

    public void Attach(IC64Cartridge cartridge)
    {
        ArgumentNullException.ThrowIfNull(cartridge);

        if (AttachedCartridge != null)
            throw new InvalidOperationException($"Cartridge '{AttachedCartridge.Name}' is already attached.");

        cartridge.Reset();
        AttachedCartridge = cartridge;
    }

    public void Detach()
    {
        var cartridge = AttachedCartridge;
        AttachedCartridge = null;
        if (cartridge == null)
            return;

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
        for (var address = (int)IO1StartAddress; address <= IO2EndAddress; address++)
        {
            var mappedAddress = (ushort)address;
            mem.MapReader(mappedAddress, ioAddress => ReadIO(ioAddress, fallbackReader));
            mem.MapWriter(mappedAddress, (ioAddress, value) => WriteIO(ioAddress, value, fallbackWriter));
        }
    }

    public void MapROMLLocations(Memory mem)
        => AttachedCartridge?.MapROMLLocations(mem);

    public void MapROMHLocations(Memory mem)
        => AttachedCartridge?.MapROMHLocations(mem);

    public void Tick()
        => AttachedCartridge?.Tick();

    public void Reset()
        => AttachedCartridge?.Reset();

    public void Dispose()
        => Detach();

    private byte ReadIO(ushort address, Func<ushort, byte> fallbackReader)
    {
        var cartridge = AttachedCartridge;
        return cartridge?.HandlesIOAddress(address) == true
            ? cartridge.ReadIO(address)
            : fallbackReader(address);
    }

    private void WriteIO(ushort address, byte value, Action<ushort, byte> fallbackWriter)
    {
        var cartridge = AttachedCartridge;
        if (cartridge?.HandlesIOAddress(address) == true)
            cartridge.WriteIO(address, value);
        else
            fallbackWriter(address, value);
    }
}
