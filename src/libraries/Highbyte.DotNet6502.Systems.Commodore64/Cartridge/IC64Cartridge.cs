namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

public interface IC64Cartridge : IDisposable
{
    string Name { get; }
    void MapIOLocations(Memory mem);
    void MapROMLLocations(Memory mem)
    {
    }
    void MapROMHLocations(Memory mem)
    {
    }
    void Tick();
    void Reset();
}
