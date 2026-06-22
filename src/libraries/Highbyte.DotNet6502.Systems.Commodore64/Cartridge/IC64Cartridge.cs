namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

public interface IC64Cartridge : IDisposable
{
    string Name { get; }
    C64CartridgeLines Lines { get; }
    event Action? LinesChanged;
    bool HandlesIOAddress(ushort address);
    byte ReadIO(ushort address);
    void WriteIO(ushort address, byte value);
    void MapROMLLocations(Memory mem)
    {
    }
    void MapROMHLocations(Memory mem)
    {
    }
    void Tick();
    void Reset();
}
