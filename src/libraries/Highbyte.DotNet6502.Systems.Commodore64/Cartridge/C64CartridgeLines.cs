namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// The active-low C64 cartridge port control lines. A high value means that the cartridge
/// releases the line; a low value means that it pulls the line active.
/// </summary>
public readonly record struct C64CartridgeLines(bool GameHigh, bool ExromHigh)
{
    public static C64CartridgeLines Released { get; } = new(GameHigh: true, ExromHigh: true);

    internal byte GetMemoryConfigurationBits()
    {
        // The C64 memory configuration table uses GAME as bit 3 and EXROM as bit 4.
        // The lower three bits come from LORAM, HIRAM, and CHAREN on the 6510 port.
        byte value = 0;
        if (GameHigh)
            value |= 0x08;
        if (ExromHigh)
            value |= 0x10;
        return value;
    }
}
