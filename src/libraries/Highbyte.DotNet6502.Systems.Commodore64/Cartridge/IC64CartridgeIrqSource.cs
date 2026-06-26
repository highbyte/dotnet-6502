namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// Optional capability for cartridges that can drive the active-low IRQ line.
/// Mirrors <see cref="IC64CartridgeNmiSource"/>; the system translates line state into
/// a CPU IRQ source so cartridges never touch <see cref="CPUInterrupts"/> directly.
/// </summary>
public interface IC64CartridgeIrqSource
{
    bool IrqLineActive { get; }
    event Action? IrqLineChanged;
}
