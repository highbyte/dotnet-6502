namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

public interface IC64Cartridge : IDisposable
{
    string Name { get; }
    C64CartridgeLines Lines { get; }
    event Action? LinesChanged;
    bool HandlesIORead(ushort address);
    byte ReadIO(ushort address);
    bool HandlesIOWrite(ushort address);
    void WriteIO(ushort address, byte value);
    bool HasROML { get; }
    byte ReadROML(ushort address);
    bool HandlesROMLWrite => false;
    void WriteROML(ushort address, byte value)
        => throw new InvalidOperationException($"{Name} does not provide writable ROML.");
    bool HasROMH { get; }
    byte ReadROMH(ushort address);
    void Tick(ulong cyclesElapsed = 0);
    void Reset();
}
