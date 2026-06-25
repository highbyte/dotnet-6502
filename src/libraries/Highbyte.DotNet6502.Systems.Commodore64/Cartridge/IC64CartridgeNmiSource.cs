namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// Optional capability for cartridges that can drive the active-low NMI line.
/// </summary>
public interface IC64CartridgeNmiSource
{
    bool NmiLineActive { get; }
    event Action? NmiLineChanged;
}
