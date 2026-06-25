namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// Optional capability for cartridges that keep the C64 in a cartridge memory
/// configuration but need selected cartridge-window reads to pass through to
/// the C64 memory map as if the cartridge lines were released.
/// </summary>
public interface IC64CartridgeNormalMemoryFallback
{
    bool UsesNormalMemoryFallbackForROMH(ushort address);
}
