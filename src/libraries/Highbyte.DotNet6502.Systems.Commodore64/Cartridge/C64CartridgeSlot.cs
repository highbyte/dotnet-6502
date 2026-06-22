namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

public sealed class C64CartridgeSlot : IDisposable
{
    public IC64Cartridge? AttachedCartridge { get; private set; }

    public void Attach(IC64Cartridge cartridge)
    {
        ArgumentNullException.ThrowIfNull(cartridge);

        if (AttachedCartridge != null)
            throw new InvalidOperationException($"Cartridge '{AttachedCartridge.Name}' is already attached.");

        AttachedCartridge = cartridge;
    }

    public void Detach()
    {
        var cartridge = AttachedCartridge;
        AttachedCartridge = null;
        cartridge?.Dispose();
    }

    public TCartridge? GetCartridge<TCartridge>()
        where TCartridge : class, IC64Cartridge
        => AttachedCartridge as TCartridge;

    public void MapIOLocations(Memory mem)
        => AttachedCartridge?.MapIOLocations(mem);

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
}
