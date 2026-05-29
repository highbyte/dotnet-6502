namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

public interface IC64CartridgeDevice
{
    string Name { get; }
    void MapIOLocations(Memory mem);
    void Tick();
    void Reset();
}
