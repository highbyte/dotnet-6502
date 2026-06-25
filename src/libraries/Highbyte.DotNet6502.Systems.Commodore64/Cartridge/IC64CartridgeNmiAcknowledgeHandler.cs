namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// Optional capability for cartridges that react when the C64 CPU acknowledges
/// an NMI, before the NMI vector is fetched.
/// </summary>
public interface IC64CartridgeNmiAcknowledgeHandler
{
    void AcknowledgeNmi();
}
